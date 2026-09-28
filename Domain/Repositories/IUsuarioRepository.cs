using Domain.Entities;
using Domain.Models;
namespace Domain.Repositories;
public interface IUsuarioRepository
{
    Task<Usuario?> VerPerfilUsuarioAsync(Guid id, CancellationToken ct);
    Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken ct);
    Task<bool> AgregarUsuarioAsync(Usuario usuario, CancellationToken ct);
    Task<bool> ExisteCiudadAsync(Guid id, CancellationToken ct);
    Task<bool> AccesoVigenteAsync(Guid id, Guid version, string rol, CancellationToken ct);
    Task RegistrarFalloAsync(Guid id, DateTime ahora, CancellationToken ct);
    Task<bool> RegistrarLoginAsync(Guid id, Guid version, DateTime ahora, CancellationToken ct);
    Task<bool> ActualizarUsuarioAsync(Guid id, UsuarioCambios cambios, CancellationToken ct);
    Task<bool> DesactivarUsuarioAsync(Guid id, CancellationToken ct);
}
