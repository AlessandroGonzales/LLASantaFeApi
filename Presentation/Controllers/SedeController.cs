using Microsoft.AspNetCore.Authorization;
using Application.DTO.Partial;
using Application.DTO.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;



namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SedeController : ControllerBase
    {
        private readonly ISedeAppService _sedeAppService;
        public SedeController(ISedeAppService sedeAppService)
        {
            _sedeAppService = sedeAppService;
        }
        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        public async Task<IActionResult> AgregarSede([FromBody] SedeRequest sedeRequest, CancellationToken cancellationToken)
        {
            await _sedeAppService.AgregarSedeAsync(sedeRequest, cancellationToken);
            return Ok(new { Mensaje = "Sede agregada exitosamente." });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ObtenerSedes(CancellationToken cancellationToken)
        {
            var sedes = await _sedeAppService.MostrarSedesAsync(cancellationToken);
            return Ok(sedes);
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> ActualizarSede(Guid id, [FromBody] SedePartial sedePartial, CancellationToken cancellationToken)
        {
            await _sedeAppService.ActualizarSedeAsync(id, sedePartial, cancellationToken);
            return Ok(new { Mensaje = "Sede actualizada exitosamente." });
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> EliminarSede(Guid id, CancellationToken cancellationToken)
        {
            await _sedeAppService.EliminarSedeAsync(id, cancellationToken);
            return Ok(new { Mensaje = "Sede eliminada exitosamente." });
        }
    }
}
