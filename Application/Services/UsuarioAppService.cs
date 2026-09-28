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
    IPasswordService passwords, ITokenService tokens, TimeProvider clock) : IUsuarioAppService
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
            Nombre = request.Nombre.Trim(), Apellido = request.Apellido.Trim(), Email = request.Email.Trim(),
            PasswordHash = passwords.Hash(request.Password), Telefono = request.Telefono?.Trim(),
            Genero = request.Genero, Profesion = request.Profesion?.Trim(), FotoPerfilUrl = request.FotoPerfilUrl,
            CiudadId = request.CiudadId, FechaNacimiento = request.FechaNacimiento, RolId = rol.Id, Activo = true
        };
        // Misma respuesta para un email existente; la unicidad final es de PostgreSQL.
        await usuarios.AgregarUsuarioAsync(usuario, ct);
    }

    public async Task<UsuarioResponse> VerPerfilUsuarioAsync(Guid id, CancellationToken ct)
    {
        var u = await usuarios.VerPerfilUsuarioAsync(id, ct) ?? throw new BusinessException("not_found", "Perfil no disponible.");
        return new()
        {
            IdUsuario = u.Id, Nombre = u.Nombre, Apellido = u.Apellido, Email = u.Email,
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

    public async Task<ParticipacionResponse> EnviarSolicitudAsync(Guid usuarioId, SolicitudCiudadanaRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (request.Mensaje.Trim().Length < 10) throw new BusinessException("validation", "El mensaje debe tener al menos 10 caracteres.");
        var id = Guid.NewGuid();
        if (!await participacion.EnviarSolicitudAsync(id, usuarioId, request.Motivo, request.Mensaje.Trim(), ct))
            throw new BusinessException("unauthorized", "Cuenta no disponible.");
        return new(id, "recibida");
    }

    private async Task ValidarCiudadAsync(Guid? ciudadId, CancellationToken ct)
    {
        if (ciudadId.HasValue && !await usuarios.ExisteCiudadAsync(ciudadId.Value, ct))
            throw new BusinessException("validation", "La ciudad indicada no existe.");
    }
}
