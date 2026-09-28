using Domain.Models;
namespace Domain.Repositories;
public interface ISolicitudesAfiliacionRepository
{
    Task<IReadOnlyList<AfiliacionPendiente>> PendientesAsync(Guid? cursor, int limite, CancellationToken ct);
    Task<AfiliacionContacto?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<AfiliacionPropia?> ObtenerPropiaAsync(Guid usuario, CancellationToken ct);
    Task<bool> AprobarAsync(Guid id, Guid admin, string? observacion, CancellationToken ct);
    Task<AfiliacionTotales> TotalesAsync(CancellationToken ct);
}
