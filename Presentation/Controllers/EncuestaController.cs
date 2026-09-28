using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Controllers;

[ApiController, Route("api/Encuesta"), Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EncuestaController(IEncuestaAppService encuestas) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirst("sub")!.Value);
    [HttpGet]
    public Task<PaginaEncuesta<EncuestaResumen>> Listar([FromQuery] Guid? cursor, CancellationToken ct, [FromQuery] int limite = 20) =>
        encuestas.ListarAsync(UsuarioId, false, cursor, limite, ct);
    [HttpGet("{id:guid}")]
    public Task<EncuestaDetalleResponse> Obtener(Guid id, CancellationToken ct) => encuestas.ObtenerAsync(id, false, ct);
    [HttpGet("administracion"), Authorize(Policy = "AdminPolicy")]
    public Task<PaginaEncuesta<EncuestaResumen>> Administracion([FromQuery] Guid? cursor, CancellationToken ct, [FromQuery] int limite = 20) =>
        encuestas.ListarAsync(UsuarioId, true, cursor, limite, ct);
    [HttpGet("administracion/{id:guid}"), Authorize(Policy = "AdminPolicy")]
    public Task<EncuestaDetalleResponse> DetalleAdministracion(Guid id, CancellationToken ct) => encuestas.ObtenerAsync(id, true, ct);
    [HttpPost, Authorize(Policy = "AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Crear(EncuestaCrearRequest request, CancellationToken ct)
    {
        var id = await encuestas.CrearAsync(request, UsuarioId, ct);
        return CreatedAtAction(nameof(DetalleAdministracion), new { id }, new { id, revision = 1L });
    }
    // Reemplazo del borrador completo: evita mezclar preguntas de dos ediciones.
    [HttpPut("{id:guid}/borrador"), Authorize(Policy = "AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Editar(Guid id, EncuestaEditarRequest request, CancellationToken ct)
    {
        await encuestas.EditarAsync(id, request, ct);
        return NoContent();
    }
    [HttpPost("{id:guid}/publicar"), Authorize(Policy = "AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Publicar(Guid id, EncuestaEstadoRequest request, CancellationToken ct)
    {
        await encuestas.CambiarEstadoAsync(id, request, true, ct);
        return NoContent();
    }
    [HttpPost("{id:guid}/cerrar"), Authorize(Policy = "AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Cerrar(Guid id, EncuestaEstadoRequest request, CancellationToken ct)
    {
        await encuestas.CambiarEstadoAsync(id, request, false, ct);
        return NoContent();
    }
    [HttpGet("{id:guid}/respuestas"), Authorize(Policy = "AdminPolicy")]
    public Task<PaginaEncuesta<RespuestaEncuestaResponse>> Respuestas(Guid id, [FromQuery] Guid? cursor, CancellationToken ct, [FromQuery] int limite = 10) =>
        encuestas.RespuestasAsync(id, cursor, limite, ct);
}
