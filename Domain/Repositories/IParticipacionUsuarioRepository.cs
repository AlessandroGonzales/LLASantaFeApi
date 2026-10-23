using Domain.Entities;
namespace Domain.Repositories;
public interface IParticipacionUsuarioRepository
{
    Task<Encuesta?> ObtenerEncuestaAsync(Guid id, CancellationToken ct);
    Task<Domain.Models.EncuestaRespuestaComprobante?> ObtenerRespuestaEncuestaAsync(Guid usuarioId, Guid encuestaId, CancellationToken ct);
    Task<bool> ResponderEncuestaAsync(Guid respuestaId, Guid usuarioId, Encuesta encuesta,
        string respuestas, DateOnly fecha, CancellationToken ct);
    Task<bool> SolicitarAfiliacionAsync(Guid solicitudId, Guid usuarioId, CancellationToken ct);
    Task<Domain.Models.SolicitudCreacion> EnviarSolicitudAsync(Guid solicitudId, Guid usuarioId, string motivo, string mensaje, Guid? clave, CancellationToken ct);
}
