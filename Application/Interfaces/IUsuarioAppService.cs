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
    Task<ParticipacionResponse> ResponderEncuestaAsync(Guid usuarioId, Guid encuestaId, EncuestaRespuestaRequest request, CancellationToken ct);
    Task<ParticipacionResponse> SolicitarAfiliacionAsync(Guid usuarioId, AfiliacionRequest request, CancellationToken ct);
    Task<ParticipacionResponse> EnviarSolicitudAsync(Guid usuarioId, SolicitudCiudadanaRequest request, CancellationToken ct);
}
