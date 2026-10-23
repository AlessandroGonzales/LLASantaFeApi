using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.Security;
namespace Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsuarioController(IUsuarioAppService usuarios) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirst("sub")!.Value); // Validado por JwtBearerEvents.

    [AllowAnonymous, HttpPost, EnableRateLimiting("registro")]
    [ApiWorkload(ApiWorkload.Authentication)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Registrar(UsuarioRequest request, CancellationToken ct)
    {
        await usuarios.AgregarUsuarioAsync(request, ct);
        return Accepted(new { Mensaje = "Solicitud procesada. Si corresponde, recibirás un correo para verificar tu cuenta." });
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    [ApiWorkload(ApiWorkload.Authentication)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await usuarios.IniciarSesionAsync(request, ct));

    [AllowAnonymous,HttpPost("email/solicitar-verificacion"),EnableRateLimiting("cuenta_correo"),ApiWorkload(ApiWorkload.Authentication)]
    public async Task<IActionResult> SolicitarVerificacion(CuentaEmailRequest request,CancellationToken ct)
    {
        await usuarios.SolicitarVerificacionAsync(request,ct);
        return Accepted(new {mensaje="Si la cuenta admite esta acción, recibirás un correo con los próximos pasos."});
    }
    [AllowAnonymous,HttpPost("email/confirmar"),EnableRateLimiting("login"),ApiWorkload(ApiWorkload.Authentication)]
    public async Task<IActionResult> ConfirmarEmail(EmailConfirmarRequest request,CancellationToken ct)
    { await usuarios.ConfirmarEmailAsync(request,ct);return NoContent(); }
    [AllowAnonymous,HttpPost("password/solicitar-recuperacion"),EnableRateLimiting("cuenta_correo"),ApiWorkload(ApiWorkload.Authentication)]
    public async Task<IActionResult> SolicitarRecuperacion(CuentaEmailRequest request,CancellationToken ct)
    {
        await usuarios.SolicitarRecuperacionAsync(request,ct);
        return Accepted(new {mensaje="Si la cuenta admite esta acción, recibirás un correo con los próximos pasos."});
    }
    [AllowAnonymous,HttpPost("password/restablecer"),EnableRateLimiting("login"),ApiWorkload(ApiWorkload.Authentication)]
    public async Task<IActionResult> RestablecerPassword(PasswordRestablecerRequest request,CancellationToken ct)
    { await usuarios.RestablecerPasswordAsync(request,ct);return NoContent(); }
    [HttpPost("me/cerrar-sesion"),EnableRateLimiting("escritura")]
    public async Task<IActionResult> CerrarSesion(CancellationToken ct)
    { await usuarios.CerrarSesionAsync(UsuarioId,Guid.Parse(User.FindFirst("ver")!.Value),ct);return NoContent(); }

    [HttpGet("me")]
    public async Task<ActionResult<UsuarioResponse>> Perfil(CancellationToken ct) =>
        Ok(await usuarios.VerPerfilUsuarioAsync(UsuarioId, ct));

    [HttpPatch("me"), EnableRateLimiting("escritura")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Actualizar(UsuarioPartial request, CancellationToken ct)
    {
        await usuarios.ActualizarUsuarioAsync(UsuarioId, request, ct);
        return NoContent();
    }

    [HttpDelete("me"), EnableRateLimiting("escritura")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Baja(CancellationToken ct)
    {
        await usuarios.DesactivarUsuarioAsync(UsuarioId, ct);
        return NoContent();
    }

    [HttpPost("me/encuestas/{encuestaId:guid}/respuesta"), EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Responder(Guid encuestaId, EncuestaRespuestaRequest request, CancellationToken ct)
    {
        var result = await usuarios.ResponderEncuestaAsync(UsuarioId, encuestaId, request, ct);
        return CreatedAtAction(nameof(RespuestaEncuesta), new { encuestaId }, result);
    }

    [HttpGet("me/encuestas/{encuestaId:guid}/respuesta")]
    [ProducesResponseType(typeof(Domain.Models.EncuestaRespuestaComprobante), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<Domain.Models.EncuestaRespuestaComprobante> RespuestaEncuesta(Guid encuestaId, CancellationToken ct) =>
        usuarios.ObtenerRespuestaEncuestaAsync(UsuarioId, encuestaId, ct);

    [HttpPost("me/afiliacion"), EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Afiliarse(AfiliacionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await usuarios.SolicitarAfiliacionAsync(UsuarioId, request, ct));

    [HttpPost("me/solicitudes"), EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnviarSolicitud(SolicitudCiudadanaRequest request,[FromHeader(Name=IdempotencyKey.Header)] string? claveIdempotencia, CancellationToken ct)
    {
        var clave=IdempotencyKey.Parse(claveIdempotencia,Request.Headers[IdempotencyKey.Header].Count);
        var result=await usuarios.EnviarSolicitudAsync(UsuarioId,request,clave,ct);
        return StatusCode(result.Nueva ? StatusCodes.Status201Created : StatusCodes.Status200OK,new ParticipacionResponse(result.Id,"recibida"));
    }
}
