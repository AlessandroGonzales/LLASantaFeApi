using System.ComponentModel.DataAnnotations;
using Application.DTO.Request;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.Security;
namespace Presentation.Controllers;

public sealed class SolicitudPdfForm
{
    [Required, FromForm(Name = "archivo")] public IFormFile Archivo { get; set; } = null!;
}
[ApiController, Route("api/SolicitudCiudadana"), Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SolicitudesCiudadanaController(ISolicitudesCiudadanaAppService solicitudes, IUsuarioAppService usuarios) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirst("sub")!.Value);
    private bool Admin => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
    [HttpPost, EnableRateLimiting("escritura")]
    public async Task<IActionResult> Crear(SolicitudCiudadanaRequest request,CancellationToken ct)
    {
        var result=await usuarios.EnviarSolicitudAsync(UsuarioId,request,ct);
        return CreatedAtAction(nameof(Obtener),new {id=result.Id},result);
    }
    [HttpGet]
    public Task<PaginaSolicitudes> MisSolicitudes([FromQuery] Guid? cursor,[FromQuery] string? estado,CancellationToken ct,[FromQuery] int limite=20) =>
        solicitudes.ListarAsync(UsuarioId,false,estado,cursor,limite,ct);
    [HttpGet("administracion"), Authorize(Policy="AdminPolicy")]
    public Task<PaginaSolicitudes> Administracion([FromQuery] Guid? cursor,[FromQuery] string? estado,CancellationToken ct,[FromQuery] int limite=20) =>
        solicitudes.ListarAsync(UsuarioId,true,estado,cursor,limite,ct);
    [HttpGet("{id:guid}")]
    public Task<SolicitudDetalle> Obtener(Guid id,CancellationToken ct) => solicitudes.ObtenerAsync(id,UsuarioId,Admin,ct);
    [HttpPatch("{id:guid}/gestion"), Authorize(Policy="AdminPolicy"), EnableRateLimiting("escritura")]
    public async Task<IActionResult> Gestionar(Guid id,SolicitudGestionRequest request,CancellationToken ct)
    {
        await solicitudes.GestionarAsync(id,UsuarioId,request,ct);
        return NoContent();
    }
    [HttpPost("{id:guid}/pdf"), Consumes("multipart/form-data"), EnableRateLimiting("escritura")]
    [RequestSizeLimit(2113536)]
    [RequestFormLimits(MultipartBodyLengthLimit=2113536,ValueCountLimit=1,ValueLengthLimit=128,MultipartHeadersCountLimit=8,MultipartHeadersLengthLimit=2048)]
    [ServiceFilter(typeof(PdfUploadQuotaFilter))]
    [ServiceFilter(typeof(PdfAccountRateFilter), Order=-1000)]
    public async Task<IActionResult> Adjuntar(Guid id,[FromForm] SolicitudPdfForm form,CancellationToken ct)
    {
        if(Request.Form.Files.Count!=1 || Request.Form.Count!=0 ||
            !string.Equals(form.Archivo.Name, "archivo", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new {mensaje="Enviá únicamente un PDF en el campo archivo."});
        await using var stream=form.Archivo.OpenReadStream();
        await solicitudes.AdjuntarAsync(id,UsuarioId,(Guid)HttpContext.Items["PdfIntento"]!,stream,
            form.Archivo.Length,form.Archivo.FileName,form.Archivo.ContentType,ct);
        return CreatedAtAction(nameof(Descargar),new {id},new {estado="disponible"});
    }
    [HttpGet("{id:guid}/pdf"), EnableRateLimiting("escritura")]
    [ServiceFilter(typeof(PdfAccountRateFilter), Order=-1000)]
    public async Task<IActionResult> Descargar(Guid id,CancellationToken ct)
    {
        var bytes=await solicitudes.DescargarAsync(id,UsuarioId,Admin,ct);
        Response.Headers.ContentSecurityPolicy="sandbox; default-src 'none'";
        return File(bytes,"application/pdf",$"solicitud-{id:N}.pdf",enableRangeProcessing:false);
    }
}
