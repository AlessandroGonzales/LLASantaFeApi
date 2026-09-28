using Application.DTO.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Controllers;
[ApiController,Route("api/Notificacion"),Authorize(Policy="AdminPolicy")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NotificacionController(INotificacionAppService service) : ControllerBase
{
    [HttpPost,EnableRateLimiting("escritura")]
    public async Task<IActionResult> Encolar(NotificacionRequest request,CancellationToken ct) =>
        Accepted(await service.EncolarAsync(request,ct));
}
