using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;
namespace Application.Interfaces;
public interface IUsuarioAppService
{
    Task<UsuarioResponse> VerPerfilUsuarioAsync(Guid id, CancellationToken ct);
    Task AgregarUsuarioAsync(UsuarioRequest usuario, CancellationToken ct);
    Task ActualizarUsuarioAsync(Guid id, UsuarioPartial cambios, CancellationToken ct);
    Task DesactivarUsuarioAsync(Guid id, CancellationToken ct);
    Task<LoginResponse> IniciarSesionAsync(LoginRequest login, CancellationToken ct);
    Task SolicitarVerificacionAsync(CuentaEmailRequest request,CancellationToken ct);
    Task ConfirmarEmailAsync(EmailConfirmarRequest request,CancellationToken ct);
    Task SolicitarRecuperacionAsync(CuentaEmailRequest request,CancellationToken ct);
    Task RestablecerPasswordAsync(PasswordRestablecerRequest request,CancellationToken ct);
    Task CerrarSesionAsync(Guid usuario,Guid version,CancellationToken ct);
    Task<ParticipacionResponse> ResponderEncuestaAsync(Guid usuarioId, Guid encuestaId, EncuestaRespuestaRequest request, CancellationToken ct);
    Task<Domain.Models.EncuestaRespuestaComprobante> ObtenerRespuestaEncuestaAsync(Guid usuarioId, Guid encuestaId, CancellationToken ct);
    Task<ParticipacionResponse> SolicitarAfiliacionAsync(Guid usuarioId, AfiliacionRequest request, CancellationToken ct);
    Task<Domain.Models.SolicitudCreacion> EnviarSolicitudAsync(Guid usuarioId, SolicitudCiudadanaRequest request, Guid? clave, CancellationToken ct);
}
