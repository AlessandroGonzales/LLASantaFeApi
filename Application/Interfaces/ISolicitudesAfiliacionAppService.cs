using Application.DTO.Request;
using Domain.Models;
namespace Application.Interfaces;
public sealed record PaginaAfiliaciones(IReadOnlyList<AfiliacionPendiente> Items, Guid? SiguienteCursor);
public interface ISolicitudesAfiliacionAppService
{
    Task<PaginaAfiliaciones> PendientesAsync(Guid? cursor, int limite, CancellationToken ct);
    Task<AfiliacionContacto> ObtenerAsync(Guid id, CancellationToken ct);
    Task<AfiliacionPropia> ObtenerPropiaAsync(Guid usuario, CancellationToken ct);
    Task AprobarAsync(Guid id, Guid admin, AfiliacionAprobarRequest request, CancellationToken ct);
    Task<AfiliacionTotales> TotalesAsync(CancellationToken ct);
}
