using Application.DTO.Request;
using Domain.Models;
namespace Application.Interfaces;
public interface INotificacionAppService
{
    Task<NotificacionEstado> EncolarAsync(NotificacionRequest request,CancellationToken ct);
}
