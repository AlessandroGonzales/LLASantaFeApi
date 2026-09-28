using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsuarioController(IUsuarioAppService usuarios) : ControllerBase
{
    private Guid UsuarioId => Guid.Parse(User.FindFirst("sub")!.Value); // Validado por JwtBearerEvents.

    [AllowAnonymous, HttpPost, EnableRateLimiting("registro")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Registrar(UsuarioRequest request, CancellationToken ct)
    {
        await usuarios.AgregarUsuarioAsync(request, ct);
        return Accepted(new { Mensaje = "Solicitud procesada. Si ya tenés una cuenta, iniciá sesión." });
    }

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await usuarios.IniciarSesionAsync(request, ct));

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
    public async Task<IActionResult> Responder(Guid encuestaId, EncuestaRespuestaRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await usuarios.ResponderEncuestaAsync(UsuarioId, encuestaId, request, ct));

    [HttpPost("me/afiliacion"), EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Afiliarse(AfiliacionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await usuarios.SolicitarAfiliacionAsync(UsuarioId, request, ct));

    [HttpPost("me/solicitudes"), EnableRateLimiting("escritura")]
    [ProducesResponseType(typeof(ParticipacionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> EnviarSolicitud(SolicitudCiudadanaRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await usuarios.EnviarSolicitudAsync(UsuarioId, request, ct));
}
