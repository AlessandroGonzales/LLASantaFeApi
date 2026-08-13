using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EfUsuario = Infrastructure.Persistence.Entities.Usuario;
namespace Infrastructure.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly LLASantaFeDbContext _db;
        public UsuarioRepository(LLASantaFeDbContext db)
        {
            _db = db;
        }

        private static EfUsuario MapToEf(Usuario usuario) => new EfUsuario
        {
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Telefono = usuario.Telefono,
            FechaNacimiento = usuario.FechaNacimiento,
            FotoPerfilUrl = usuario.FotoPerfilUrl,
            Profesion = usuario.Profesion,
            Genero = usuario.Genero,
            PasswordHash = usuario.PasswordHash,
            RolId = usuario.RolId,
        };

        public async Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken cancellationToken)
        {
            var efUsuario = await _db.Usuarios
                .AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => new Usuario
                {
                    Nombre = u.Nombre,
                    RoleNombre = u.Rol.Nombre,
                    PasswordHash = u.PasswordHash,
                }).FirstOrDefaultAsync(cancellationToken);
            return efUsuario;
        }

        public async Task<Usuario?> VerPerfilUsuarioAsync(Guid id, CancellationToken cancellationToken)
        {
            var efUsuario = await _db.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new Usuario
                {
                    Nombre = u.Nombre,
                    Apellido = u.Apellido,
                    Email = u.Email,
                    Telefono = u.Telefono,
                    FechaNacimiento = u.FechaNacimiento,
                    FotoPerfilUrl = u.FotoPerfilUrl,
                    Profesion = u.Profesion,
                    Genero = u.Genero,
                    FechaRegistro = u.FechaRegistro,
                }).FirstOrDefaultAsync(cancellationToken);

            return efUsuario;
        }

        public async Task AgregarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            var efUsuario = MapToEf(usuario);
            await _db.Usuarios.AddAsync(efUsuario, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ActualizarUsuarioAsync(Guid id, Usuario updatedUsuario, CancellationToken cancellationToken)
        {
            await _db.Usuarios
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Nombre, updatedUsuario.Nombre)
                .SetProperty(u => u.Apellido, updatedUsuario.Apellido)
                .SetProperty(u => u.Telefono, updatedUsuario.Telefono)
                .SetProperty(u => u.CiudadId, updatedUsuario.CiudadId)
                .SetProperty(u => u.FotoPerfilUrl, updatedUsuario.FotoPerfilUrl)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
        }

        public async Task DesactivarUsuarioAsync(Guid id, CancellationToken cancellationToken)
        {
            await _db.Usuarios
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Activo, false)
                .SetProperty(u => u.DeletedAt, DateTime.UtcNow)
                .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
        }
    }
}
