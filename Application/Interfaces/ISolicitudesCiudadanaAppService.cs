using Application.DTO.Request;
using Domain.Models;
namespace Application.Interfaces;
public sealed record PaginaSolicitudes(IReadOnlyList<SolicitudVista> Items, Guid? SiguienteCursor);
public interface ISolicitudesCiudadanaAppService
{
    Task<PaginaSolicitudes> ListarAsync(Guid usuario, bool admin, string? estado, Guid? cursor, int limite, CancellationToken ct);
    Task<SolicitudDetalle> ObtenerAsync(Guid id, Guid usuario, bool admin, CancellationToken ct);
    Task GestionarAsync(Guid id, Guid admin, SolicitudGestionRequest request, CancellationToken ct);
    Task<Guid> ReservarPdfAsync(Guid id, Guid usuario, CancellationToken ct);
    Task AdjuntarAsync(Guid id, Guid usuario, Guid intento, Stream contenido, long length, string nombre, string contentType, CancellationToken ct);
    Task<byte[]> DescargarAsync(Guid id, Guid usuario, bool admin, CancellationToken ct);
}
public interface IPdfScanner
{
    Task<bool> EsLimpioAsync(byte[] contenido, CancellationToken ct);
}
