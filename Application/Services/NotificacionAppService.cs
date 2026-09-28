using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class NotificacionAppService(INotificacionRepository repository) : INotificacionAppService
{
    public async Task<NotificacionEstado> EncolarAsync(NotificacionRequest request,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        return await repository.EncolarAsync(request.Tipo,request.ReferenciaId,ct)
            ?? throw new BusinessException("not_found","No existe un evento válido para esa notificación.");
    }
}
