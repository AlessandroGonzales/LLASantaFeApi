using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class NotificacionAppService(INotificacionRepository repository, ICorreoWakeSignal correoWake) : INotificacionAppService
{
    public async Task<NotificacionEstado> EncolarAsync(NotificacionRequest request,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        var result=await repository.EncolarAsync(request.Tipo,request.ReferenciaId,ct)
            ?? throw new BusinessException("not_found","No existe un evento válido para esa notificación.");
        if (result.Estado=="pendiente") correoWake.Notify();
        return result;
    }
}
