using Domain.Models;
namespace Domain.Repositories;
public interface ISolicitudesCiudadanaRepository
{
    Task<IReadOnlyList<SolicitudVista>> ListarAsync(Guid usuario, bool admin, string? estado, Guid? cursor, int limite, CancellationToken ct);
    Task<SolicitudDetalle?> ObtenerAsync(Guid id, Guid usuario, bool admin, CancellationToken ct);
    Task<bool> GestionarAsync(Guid id, Guid admin, long revision, string estado, string? respuesta, CancellationToken ct);
    Task<SolicitudPdfReserva> ReservarPdfAsync(Guid id, Guid usuario, Guid? clave, CancellationToken ct);
    Task<bool> GuardarPdfAsync(Guid id, Guid usuario, Guid intento, byte[] contenido, string hash, CancellationToken ct);
    Task FinalizarIntentoPdfAsync(Guid intento, Guid usuario, CancellationToken ct);
    Task<SolicitudPdfDatos?> ObtenerPdfAsync(Guid id, Guid usuario, bool admin, CancellationToken ct);
}
