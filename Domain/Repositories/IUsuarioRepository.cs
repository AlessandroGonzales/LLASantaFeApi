using Domain.Entities;
using Domain.Models;
namespace Domain.Repositories;
public interface IUsuarioRepository
{
    Task<Usuario?> VerPerfilUsuarioAsync(Guid id, CancellationToken ct);
    Task<Usuario?> ObtenerUsuarioPorEmailAsync(string email, CancellationToken ct);
    Task<bool> AgregarUsuarioAsync(Usuario usuario, Guid accionId,string huella,int venceMinutes,CancellationToken ct);
    Task<bool> SolicitarAccionAsync(string email,string tipo,Guid accionId,string huella,int venceMinutes,CancellationToken ct);
    Task<CuentaAccion?> ObtenerAccionAsync(Guid id,string tipo,string huella,CancellationToken ct);
    Task<bool> ConfirmarEmailAsync(Guid usuario,Guid id,string huella,CancellationToken ct);
    Task<bool> RestablecerPasswordAsync(Guid id,string huella,string passwordHash,CancellationToken ct);
    Task<bool> CerrarSesionAsync(Guid usuario,Guid version,CancellationToken ct);
    Task<bool> ExisteCiudadAsync(Guid id, CancellationToken ct);
    Task<bool> AccesoVigenteAsync(Guid id, Guid version, string rol, CancellationToken ct);
    Task RegistrarFalloAsync(Guid id, DateTime ahora, CancellationToken ct);
    Task<bool> RegistrarLoginAsync(Guid id, Guid version, DateTime ahora, CancellationToken ct);
    Task<bool> ActualizarUsuarioAsync(Guid id, UsuarioCambios cambios, CancellationToken ct);
    Task<bool> DesactivarUsuarioAsync(Guid id, CancellationToken ct);
}
