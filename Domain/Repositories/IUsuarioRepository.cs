using Domain.Entities;

namespace Domain.Repositories
{
    public interface IUsuarioRepository
    {
        Task<Usuario?> VerPerfilUsuarioAsync(Guid id, CancellationToken cancellationToken);
        Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken cancellationToken);
        Task AgregarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken);
        Task ActualizarUsuarioAsync(Guid id, Usuario updatedUsuario, CancellationToken cancellationToken);
        Task DesactivarUsuarioAsync(Guid id, CancellationToken cancellationToken);
    }
}
