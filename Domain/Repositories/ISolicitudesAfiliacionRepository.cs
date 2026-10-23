using Domain.Models;
namespace Domain.Repositories;
public interface ISolicitudesAfiliacionRepository
{
    Task<IReadOnlyList<AfiliacionPendiente>> PendientesAsync(string estado, Guid? cursor, int limite, CancellationToken ct);
    Task<bool> ExisteAsync(Guid id, CancellationToken ct);
    Task<AfiliacionContacto?> ObtenerAsync(Guid id, CancellationToken ct);
    Task<AfiliacionPropia?> ObtenerPropiaAsync(Guid usuario, CancellationToken ct);
    Task<bool> AprobarAsync(Guid id, Guid admin, string? observacion, CancellationToken ct);
    Task<bool> RechazarAsync(Guid id, Guid admin, string motivo, CancellationToken ct);
    Task<AfiliacionTotales> TotalesAsync(CancellationToken ct);
}
