using Application.Authentication;
using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Application.Validation;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Repositories;
namespace Application.Services;
public sealed class UsuarioAppService(
    IUsuarioRepository usuarios, IRoleRepository roles, IParticipacionUsuarioRepository participacion,
    IPasswordService passwords, ITokenService tokens, TimeProvider clock, ICorreoWakeSignal correoWake,
    ICodigoCuentaService codigos,CuentaSecuritySettings cuentaSettings) : IUsuarioAppService
{
    private static readonly TimeZoneInfo SantaFe = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    public async Task<LoginResponse> IniciarSesionAsync(LoginRequest login, CancellationToken ct)
    {
        UsuarioRules.Validate(login);
        var usuario = await usuarios.ObtenerUsuarioPorEmailAsync(login.Email, ct);
        var matches = passwords.Verify(login.Password, usuario?.PasswordHash);
        var now = clock.GetUtcNow().UtcDateTime;
        if (usuario is null || !matches || !usuario.Activo || usuario.DeletedAt is not null ||
            usuario.BloqueadoHasta > now || string.IsNullOrWhiteSpace(usuario.RoleNombre))
        {
            if (usuario is not null && !matches) await usuarios.RegistrarFalloAsync(usuario.Id, now, ct);
            throw new BusinessException("unauthorized", "Credenciales inválidas.");
        }
        if (usuario.RequiereVerificarEmail && usuario.EmailVerificadoAt is null)
            throw new BusinessException("email_not_verified","Verificá tu email antes de iniciar sesión.");
        if (!await usuarios.RegistrarLoginAsync(usuario.Id, usuario.VersionAcceso, now, ct))
            throw new BusinessException("unauthorized", "Credenciales inválidas.");
        return tokens.Create(usuario);
    }

    public async Task AgregarUsuarioAsync(UsuarioRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        await ValidarCiudadAsync(request.CiudadId, ct);
        var rol = await roles.ObtenerRolIdPorNombreAsync("Usuario", ct);
        // El cliente nunca elige el rol, estado, fecha de registro ni hash.
        var usuario = new Usuario
        {
            Id=Guid.NewGuid(),Nombre = request.Nombre.Trim(), Apellido = request.Apellido.Trim(), Email = request.Email.Trim(),
            PasswordHash = passwords.Hash(request.Password), Telefono = request.Telefono?.Trim(),
            Genero = request.Genero, Profesion = request.Profesion?.Trim(), FotoPerfilUrl = request.FotoPerfilUrl,
            CiudadId = request.CiudadId, FechaNacimiento = request.FechaNacimiento, RolId = rol.Id, Activo = true
        };
        // Misma respuesta para un email existente; la unicidad final es de PostgreSQL.
        var accion=Guid.NewGuid();
        if (await usuarios.AgregarUsuarioAsync(usuario,accion,codigos.Huella(codigos.Crear(accion,"email_verificacion")),cuentaSettings.VerificacionMinutes,ct)) correoWake.Notify();
    }

    public async Task<UsuarioResponse> VerPerfilUsuarioAsync(Guid id, CancellationToken ct)
    {
        var u = await usuarios.VerPerfilUsuarioAsync(id, ct) ?? throw new BusinessException("not_found", "Perfil no disponible.");
        return new()
        {
            IdUsuario = u.Id, Nombre = u.Nombre, Apellido = u.Apellido, Email = u.Email,
            EmailVerificado=u.EmailVerificadoAt is not null,RequiereVerificarEmail=u.RequiereVerificarEmail,
            Telefono = u.Telefono, FechaNacimiento = u.FechaNacimiento, Genero = u.Genero,
            Profesion = u.Profesion, FotoPerfilUrl = u.FotoPerfilUrl, CiudadId = u.CiudadId,
            RoleNombre = u.RoleNombre, FechaRegistro = u.FechaRegistro
        };
    }

    public async Task ActualizarUsuarioAsync(Guid id, UsuarioPartial request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        await ValidarCiudadAsync(request.CiudadId, ct);
        if (!await usuarios.ActualizarUsuarioAsync(id, request.ToChanges(), ct))
            throw new BusinessException("not_found", "Perfil no disponible.");
    }


    public async Task DesactivarUsuarioAsync(Guid id, CancellationToken ct)
    {
        if (!await usuarios.DesactivarUsuarioAsync(id, ct))
            throw new BusinessException("not_found", "Perfil no disponible.");
    }

    public async Task<Domain.Models.EncuestaRespuestaComprobante> ObtenerRespuestaEncuestaAsync(Guid usuarioId, Guid encuestaId, CancellationToken ct) =>
        await participacion.ObtenerRespuestaEncuestaAsync(usuarioId, encuestaId, ct) ??
            throw new BusinessException("not_found", "No hay una respuesta registrada para tu cuenta en esta encuesta.");

    public async Task<ParticipacionResponse> ResponderEncuestaAsync(Guid usuarioId, Guid encuestaId, EncuestaRespuestaRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        var encuesta = await participacion.ObtenerEncuestaAsync(encuestaId, ct) ??
            throw new BusinessException("not_found", "Encuesta no disponible.");
        var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), SantaFe).DateTime);
        if (!encuesta.Activa || encuesta.PublicadaAt is null || encuesta.FechaInicio > fecha || encuesta.FechaFin < fecha)
            throw new BusinessException("conflict", "La encuesta no admite respuestas.");
        var answers = EncuestaValidator.Validate(encuesta.Configuracion, request.Respuestas);
        var id = Guid.NewGuid();
        if (!await participacion.ResponderEncuestaAsync(id, usuarioId, encuesta, answers, fecha, ct))
            throw new BusinessException("conflict", "La encuesta o la cuenta cambió. Volvé a consultar su estado.");
        return new(id, "registrada");
    }

    public async Task<ParticipacionResponse> SolicitarAfiliacionAsync(Guid usuarioId, AfiliacionRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        var id = Guid.NewGuid();
        if (!await participacion.SolicitarAfiliacionAsync(id, usuarioId, ct))
            throw new BusinessException("unauthorized", "Cuenta no disponible.");
        return new(id, "pendiente");
    }

    public async Task<Domain.Models.SolicitudCreacion> EnviarSolicitudAsync(Guid usuarioId, SolicitudCiudadanaRequest request, Guid? clave, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (request.Mensaje.Trim().Length < 10 || request.Mensaje.Contains('\0') || clave == Guid.Empty)
            throw new BusinessException("validation", "Mensaje o clave de idempotencia inválidos.");
        return await participacion.EnviarSolicitudAsync(Guid.NewGuid(), usuarioId, request.Motivo, request.Mensaje.Trim(), clave, ct);
    }

    private async Task ValidarCiudadAsync(Guid? ciudadId, CancellationToken ct)
    {
        if (ciudadId.HasValue && !await usuarios.ExisteCiudadAsync(ciudadId.Value, ct))
            throw new BusinessException("validation", "La ciudad indicada no existe.");
    }

    public Task SolicitarVerificacionAsync(CuentaEmailRequest request,CancellationToken ct) => SolicitarCodigoAsync(request,"email_verificacion",cuentaSettings.VerificacionMinutes,ct);
    public Task SolicitarRecuperacionAsync(CuentaEmailRequest request,CancellationToken ct) => SolicitarCodigoAsync(request,"password_recuperacion",cuentaSettings.RecuperacionMinutes,ct);
    private async Task SolicitarCodigoAsync(CuentaEmailRequest request,string tipo,int minutes,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        var start=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var id=Guid.NewGuid();
            if (await usuarios.SolicitarAccionAsync(request.Email.Trim(),tipo,id,codigos.Huella(codigos.Crear(id,tipo)),minutes,ct)) correoWake.Notify();
        }
        finally
        {
            // Piso breve con jitter para no exponer una salida rápida por email inexistente.
            var floor=TimeSpan.FromMilliseconds(200+System.Security.Cryptography.RandomNumberGenerator.GetInt32(25));
            var remaining=floor-System.Diagnostics.Stopwatch.GetElapsedTime(start);
            if (remaining>TimeSpan.Zero) await Task.Delay(remaining,ct);
        }
    }
    public async Task ConfirmarEmailAsync(EmailConfirmarRequest request,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (!LeerCodigo(request.Codigo,"email_verificacion",out var id,out var hash)) throw CodigoInvalido();
        var action=await usuarios.ObtenerAccionAsync(id,"email_verificacion",hash,ct);
        var user=await usuarios.ObtenerUsuarioPorEmailAsync(request.Email,ct);
        var matches=passwords.Verify(request.Password,user?.PasswordHash);
        // La contraseña vincula la confirmación al registro que hizo la persona, evitando confirmar una cuenta creada por terceros.
        if (!matches || user is null || action is null || action.UsuarioId!=user.Id ||
            !await usuarios.ConfirmarEmailAsync(user.Id,id,hash,ct)) throw CodigoInvalido();
        correoWake.Notify();
    }
    public async Task RestablecerPasswordAsync(PasswordRestablecerRequest request,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (!LeerCodigo(request.Codigo,"password_recuperacion",out var id,out var hash) ||
            await usuarios.ObtenerAccionAsync(id,"password_recuperacion",hash,ct) is null) throw CodigoInvalido();
        var passwordHash=passwords.Hash(request.NuevaPassword);
        if (!await usuarios.RestablecerPasswordAsync(id,hash,passwordHash,ct)) throw CodigoInvalido();
        correoWake.Notify();
    }
    public async Task CerrarSesionAsync(Guid usuario,Guid version,CancellationToken ct)
    {
        if (!await usuarios.CerrarSesionAsync(usuario,version,ct)) throw new BusinessException("unauthorized","Acceso no vigente.");
    }
    private bool LeerCodigo(string codigo,string tipo,out Guid id,out string huella)
    {
        if (!codigos.Leer(codigo,out id,out huella)) return false;
        var expected=codigos.Huella(codigos.Crear(id,tipo));
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Convert.FromHexString(huella),Convert.FromHexString(expected));
    }
    private static BusinessException CodigoInvalido() => new("validation","Código inválido, vencido o ya utilizado.");
}
