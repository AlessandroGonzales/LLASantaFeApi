using Domain.Models;
namespace Domain.Repositories;
public interface INotificacionRepository
{
    Task<NotificacionEstado?> EncolarAsync(string tipo,Guid referencia,CancellationToken ct);
    Task<CorreoPendiente?> ReservarAsync(int limiteDiario,CancellationToken ct);
    Task FinalizarAsync(Guid id,string estado,string? proveedorId,string? error,int esperaSegundos,CancellationToken ct);
}
