using Microsoft.AspNetCore.Authorization;
using Application.Interfaces;
using Application.DTO.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.Security;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CiudadController : ControllerBase
    {
        private readonly ICiudadAppService _ciudadAppService;
        private readonly IOutputCacheStore cache;
        private readonly CatalogCacheRevision revision;
        public CiudadController(ICiudadAppService ciudadAppService,IOutputCacheStore cache,CatalogCacheRevision revision)
        {
            _ciudadAppService = ciudadAppService;
            this.cache=cache;
            this.revision=revision;
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        [EnableRateLimiting("escritura")]
        public async Task<IActionResult> AgregarCiudad([FromQuery] string nombre,[FromQuery] Guid departamentoId, CancellationToken cancellationToken)
        {
            await _ciudadAppService.AgregarCiudadAsync(nombre, departamentoId, cancellationToken);
            revision.Changed(PublicCatalogCache.GeographyTag);
            await cache.EvictByTagAsync(PublicCatalogCache.GeographyTag,cancellationToken);
            return Ok(new { Mensaje = "Ciudad agregada exitosamente." });
        }

        [AllowAnonymous]
        [HttpGet]
        [OutputCache(PolicyName=PublicCatalogCache.Cities)]
        [ProducesResponseType(typeof(IEnumerable<CiudadResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ObtenerCiudadesPorDepartamentoId([FromQuery] Guid departamentoId, CancellationToken cancellationToken)
        {
            var ciudades = await _ciudadAppService.ObtenerCiudadesPorDepartamentoIdAsync(departamentoId, cancellationToken);
            return Ok(ciudades);
        }
    }
}
