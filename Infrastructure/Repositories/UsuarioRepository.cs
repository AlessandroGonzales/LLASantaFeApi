using Domain.Entities;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EfUsuario = Infrastructure.Persistence.Entities.Usuario;
namespace Infrastructure.Repositories;
public sealed class UsuarioRepository(LLASantaFeDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Usuarios.AsNoTracking().Where(u => u.EmailNormalizado == normalized)
            .Select(u => new Usuario
            {
                Id = u.Id, Nombre = u.Nombre, RoleNombre = u.Rol == null ? null : u.Rol.Nombre,
                PasswordHash = u.PasswordHash, Activo = u.Activo, DeletedAt = u.DeletedAt,
                VersionAcceso = u.VersionAcceso, IntentosFallidos = u.IntentosFallidos, BloqueadoHasta = u.BloqueadoHasta,
                EmailVerificadoAt=u.EmailVerificadoAt,RequiereVerificarEmail=u.RequiereVerificarEmail
            }).SingleOrDefaultAsync(ct);
    }
    public Task<Usuario?> VerPerfilUsuarioAsync(Guid id, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().Where(u => u.Id == id && u.Activo && u.DeletedAt == null)
            .Select(u => new Usuario
            {
                Id = u.Id, Nombre = u.Nombre, Apellido = u.Apellido, Email = u.Email,
                Telefono = u.Telefono, FechaNacimiento = u.FechaNacimiento, FotoPerfilUrl = u.FotoPerfilUrl,
                Profesion = u.Profesion, Genero = u.Genero, FechaRegistro = u.FechaRegistro,
                CiudadId = u.CiudadId, RoleNombre = u.Rol == null ? null : u.Rol.Nombre,
                EmailVerificadoAt=u.EmailVerificadoAt,RequiereVerificarEmail=u.RequiereVerificarEmail
            }).SingleOrDefaultAsync(ct);

    public Task<bool> ExisteCiudadAsync(Guid id, CancellationToken ct) => db.Ciudades.AnyAsync(c => c.Id == id, ct);
    public Task<bool> AccesoVigenteAsync(Guid id, Guid version, string rol, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.Id == id && u.Activo && u.DeletedAt == null &&
            u.VersionAcceso == version && u.Rol != null && u.Rol.Nombre == rol &&
            (!u.RequiereVerificarEmail || u.EmailVerificadoAt!=null), ct);

    public async Task<bool> AgregarUsuarioAsync(Usuario usuario,Guid accionId,string huella,int venceMinutes,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        db.Usuarios.Add(new EfUsuario
        {
            Id = usuario.Id, Nombre = usuario.Nombre, Apellido = usuario.Apellido, Email = usuario.Email,
            Telefono = usuario.Telefono, FechaNacimiento = usuario.FechaNacimiento, FotoPerfilUrl = usuario.FotoPerfilUrl,
            Profesion = usuario.Profesion, Genero = usuario.Genero, PasswordHash = usuario.PasswordHash,
            RolId = usuario.RolId, CiudadId = usuario.CiudadId, Activo = true,RequiereVerificarEmail=true
        });
        try
        {
            await db.SaveChangesAsync(ct);
            if (!await SolicitarAccionAsync(usuario.Email,"email_verificacion",accionId,huella,venceMinutes,ct))
                throw new InvalidOperationException("No se pudo preparar la verificación.");
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "usuarios_email_normalizado_key" or "usuarios_email_key" })
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task RegistrarFalloAsync(Guid id, DateTime ahora, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == id && u.Activo && u.DeletedAt == null &&
                (u.BloqueadoHasta == null || u.BloqueadoHasta <= ahora))
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.IntentosFallidos, u => u.BloqueadoHasta != null && u.BloqueadoHasta <= ahora ? 1 : u.IntentosFallidos + 1)
                .SetProperty(u => u.BloqueadoHasta, u =>
                    (u.BloqueadoHasta != null && u.BloqueadoHasta <= ahora ? 1 : u.IntentosFallidos + 1) >= 5
                        ? ahora.AddMinutes(5) : (DateTime?)null), ct);

    public async Task<bool> RegistrarLoginAsync(Guid id, Guid version, DateTime ahora, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == id && u.VersionAcceso == version && u.Activo && u.DeletedAt == null &&
                (!u.RequiereVerificarEmail || u.EmailVerificadoAt!=null) &&
                (u.BloqueadoHasta == null || u.BloqueadoHasta <= ahora))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IntentosFallidos, 0)
                .SetProperty(u => u.BloqueadoHasta, (DateTime?)null), ct) == 1;

    public async Task<bool> ActualizarUsuarioAsync(Guid id, UsuarioCambios c, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == id && u.Activo && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Nombre, u => c.Nombre.Incluido ? c.Nombre.Valor! : u.Nombre)
                .SetProperty(u => u.Apellido, u => c.Apellido.Incluido ? c.Apellido.Valor! : u.Apellido)
                .SetProperty(u => u.Telefono, u => c.Telefono.Incluido ? c.Telefono.Valor : u.Telefono)
                .SetProperty(u => u.CiudadId, u => c.CiudadId.Incluido ? c.CiudadId.Valor : u.CiudadId)
                .SetProperty(u => u.FotoPerfilUrl, u => c.FotoPerfilUrl.Incluido ? c.FotoPerfilUrl.Valor : u.FotoPerfilUrl), ct) == 1;

    public async Task<bool> DesactivarUsuarioAsync(Guid id, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == id && u.Activo && u.DeletedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Activo, false)
                .SetProperty(u => u.DeletedAt, DateTime.UtcNow), ct) == 1;

    public async Task<bool> SolicitarAccionAsync(string email,string tipo,Guid accionId,string huella,int venceMinutes,CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"SELECT public.solicitar_accion_usuario({email},{tipo},{accionId},{huella},{venceMinutes}) AS \"Value\"").SingleAsync(ct);
    public Task<CuentaAccion?> ObtenerAccionAsync(Guid id,string tipo,string huella,CancellationToken ct) =>
        db.Database.SqlQuery<CuentaAccion>($"""
            SELECT a.usuario_id AS "UsuarioId",a.version_acceso AS "VersionAcceso"
            FROM public.usuario_acciones a JOIN public.usuarios u ON u.id=a.usuario_id
            WHERE a.id={id} AND a.tipo={tipo} AND a.codigo_hash={huella} AND a.usado_at IS NULL AND a.vence_at>now()
             AND u.activo AND u.deleted_at IS NULL AND a.version_acceso=u.version_acceso
            """).SingleOrDefaultAsync(ct);
    public async Task<bool> ConfirmarEmailAsync(Guid usuario,Guid id,string huella,CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"SELECT public.consumir_accion_usuario({id},'email_verificacion',{huella},{usuario},NULL::text) AS \"Value\"").SingleAsync(ct);
    public async Task<bool> RestablecerPasswordAsync(Guid id,string huella,string passwordHash,CancellationToken ct) =>
        await db.Database.SqlQuery<bool>($"SELECT public.consumir_accion_usuario({id},'password_recuperacion',{huella},NULL::uuid,{passwordHash}) AS \"Value\"").SingleAsync(ct);
    public async Task<bool> CerrarSesionAsync(Guid usuario,Guid version,CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.usuarios SET version_acceso=public.uuid_generate_v4()
            WHERE id={usuario} AND version_acceso={version} AND activo AND deleted_at IS NULL
            """,ct)==1;
}
