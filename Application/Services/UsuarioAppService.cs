using Application.DTO.Response;
using Application.Interfaces;
using Domain.Repositories;
using Domain.Entities;
using Application.DTO.Request;
using Application.DTO.Partial;

namespace Application.Services
{
    public class UsuarioAppService : IUsuarioAppService
    {
        private readonly IUsuarioRepository _repo;
        private readonly IRoleRepository _repoRole;
        public UsuarioAppService(IUsuarioRepository repo, IRoleRepository repoRole)
        {
            _repo = repo;
            _repoRole = repoRole;
        }

        public async Task<UsuarioResponse?> ValidarUsuarioAsync(string email, string password, CancellationToken cancellationToken)
        {
            var usuario = await _repo.ObtenerUsuarioPorEmailAsync(email, cancellationToken);
            if (usuario is null || !BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash))
            {
                throw new UnauthorizedAccessException("Credenciales inválidas");
            }

            return usuario is null ? null : new UsuarioResponse
            {
                Nombre = usuario.Nombre,
                RoleNombre = usuario.RoleNombre,
            };
        }

        public async Task<UsuarioResponse?> VerPerfilUsuarioAsync(Guid id, CancellationToken cancellationToken) 
        {
            var usuario = await _repo.VerPerfilUsuarioAsync(id, cancellationToken);
            return usuario is null ? null : new UsuarioResponse
            {
                Nombre = usuario.Nombre,
                Apellido = usuario.Apellido,
                Email = usuario.Email,
                Telefono = usuario.Telefono,
                Genero = usuario.Genero,
                Profesion = usuario.Profesion,
                FotoPerfilUrl = usuario.FotoPerfilUrl,
                CiudadId = usuario.CiudadId,
                RoleNombre = usuario.RoleNombre
            };
        }

        public async Task AgregarUsuarioAsync(UsuarioRequest usuarioRequest, CancellationToken cancellationToken)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(usuarioRequest.Password);
            var rolId = await _repoRole.ObtenerRolIdPorNombreAsync("Usuario", cancellationToken);

            await _repo.AgregarUsuarioAsync(new Usuario
            {
                Nombre = usuarioRequest.Nombre,
                Apellido = usuarioRequest.Apellido,
                Email = usuarioRequest.Email,
                PasswordHash = passwordHash,
                Telefono = usuarioRequest.Telefono,
                Genero = usuarioRequest.Genero,
                Profesion = usuarioRequest.Profesion,
                FotoPerfilUrl = usuarioRequest.FotoPerfilUrl,
                CiudadId = usuarioRequest.CiudadId,
                FechaNacimiento = usuarioRequest.FechaNacimiento,
                RolId = rolId.Id

            }, cancellationToken);
        }

        public async Task ActualizarUsuarioAsync(Guid id, UsuarioPartial updatedUsuario, CancellationToken cancellationToken)
        {
            await _repo.ActualizarUsuarioAsync(id, new Usuario
            {
                Nombre = updatedUsuario.Nombre,
                Apellido = updatedUsuario.Apellido,
                Telefono = updatedUsuario.Telefono,
                CiudadId = updatedUsuario.CiudadId,
                FotoPerfilUrl = updatedUsuario.FotoPerfilUrl
            }, cancellationToken);
        }

        public async Task DesactivarUsuarioAsync(Guid id, CancellationToken cancellationToken)
        {
            await _repo.DesactivarUsuarioAsync(id, cancellationToken);    
        }

    }
}
