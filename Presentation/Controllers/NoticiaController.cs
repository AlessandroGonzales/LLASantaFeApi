using Application.DTO.Partial;
using Application.DTO.Request;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Controllers;
[ApiController,Route("api/Noticia")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NoticiaController(INoticiaAppService noticias) : ControllerBase
{
    [HttpPost,Authorize(Policy="AdminPolicy"),EnableRateLimiting("escritura")]
    public async Task<IActionResult> Crear(NoticiaCrearRequest request,CancellationToken ct)
    {
        var id=await noticias.CrearAsync(request,Guid.Parse(User.FindFirst("sub")!.Value),ct);
        return CreatedAtAction(nameof(DetalleAdministracion),new {id},new {id});
    }
    [HttpGet,AllowAnonymous]
    public Task<IReadOnlyList<NoticiaDatos>> Ultimas(CancellationToken ct)=>noticias.UltimasAsync(ct);
    [HttpGet("administracion/{id:guid}"),Authorize(Policy="AdminPolicy")]
    public Task<NoticiaDatos> DetalleAdministracion(Guid id,CancellationToken ct)=>noticias.ObtenerAsync(id,ct);
    [HttpPatch("{id:guid}"),Authorize(Policy="AdminPolicy"),EnableRateLimiting("escritura")]
    public async Task<IActionResult> Actualizar(Guid id,NoticiaPartial request,CancellationToken ct)
    {
        await noticias.ActualizarAsync(id,request,ct);
        return NoContent();
    }
}
