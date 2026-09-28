using Microsoft.AspNetCore.Authorization;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CiudadController : ControllerBase
    {
        private readonly ICiudadAppService _ciudadAppService;
        public CiudadController(ICiudadAppService ciudadAppService)
        {
            _ciudadAppService = ciudadAppService;
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        public async Task<IActionResult> AgregarCiudad([FromQuery] string nombre,[FromQuery] Guid departamentoId, CancellationToken cancellationToken)
        {
            await _ciudadAppService.AgregarCiudadAsync(nombre, departamentoId, cancellationToken);
            return Ok(new { Mensaje = "Ciudad agregada exitosamente." });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ObtenerCiudadesPorDepartamentoId([FromQuery] Guid departamentoId, CancellationToken cancellationToken)
        {
            var ciudades = await _ciudadAppService.ObtenerCiudadesPorDepartamentoIdAsync(departamentoId, cancellationToken);
            return Ok(ciudades);
        }
    }
}
