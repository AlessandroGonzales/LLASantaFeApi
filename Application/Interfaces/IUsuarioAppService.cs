using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;

namespace Application.Interfaces
{
    public interface IUsuarioAppService
    {
        Task<UsuarioResponse?> VerPerfilUsuarioAsync(Guid id, CancellationToken cancellationToken);
        Task AgregarUsuarioAsync(UsuarioRequest usuario, CancellationToken cancellationToken);
        Task ActualizarUsuarioAsync(Guid id, UsuarioPartial updatedUsuario, CancellationToken cancellationToken);
        Task DesactivarUsuarioAsync(Guid id, CancellationToken cancellationToken);
        Task<UsuarioResponse?> ValidarUsuarioAsync(string email, string password, CancellationToken cancellationToken);
    }
}
