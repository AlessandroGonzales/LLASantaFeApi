using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Controllers;
[ApiController, Route("api/SolicitudAfiliacion"), Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SolicitudesAfiliacionController(ISolicitudesAfiliacionAppService afiliaciones,IUsuarioAppService usuarios) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpPost, EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse),StatusCodes.Status201Created)]
    public async Task<IActionResult> Solicitar(AfiliacionRequest request,CancellationToken ct)
    {
        var result=await usuarios.SolicitarAfiliacionAsync(UsuarioId,request,ct);
        return CreatedAtAction(nameof(MiSolicitud),result);
    }
    [HttpGet("me")]
    public Task<AfiliacionPropia> MiSolicitud(CancellationToken ct) => afiliaciones.ObtenerPropiaAsync(UsuarioId,ct);
    [HttpGet("pendientes"), Authorize(Policy="AdminPolicy")]
    public Task<PaginaAfiliaciones> Pendientes([FromQuery] Guid? cursor,CancellationToken ct,[FromQuery] int limite=20) =>
        afiliaciones.PendientesAsync(cursor,limite,ct);
    [HttpGet("{id:guid}"), Authorize(Policy="AdminPolicy")]
    public Task<AfiliacionContacto> Detalle(Guid id,CancellationToken ct) => afiliaciones.ObtenerAsync(id,ct);
    [HttpPost("{id:guid}/aprobar"), Authorize(Policy="AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Aprobar(Guid id,AfiliacionAprobarRequest request,CancellationToken ct)
    {
        await afiliaciones.AprobarAsync(id,UsuarioId,request,ct);
        return NoContent();
    }
    [HttpGet("totales"), Authorize(Policy="AdminPolicy")]
    public Task<AfiliacionTotales> Totales(CancellationToken ct) => afiliaciones.TotalesAsync(ct);
}
