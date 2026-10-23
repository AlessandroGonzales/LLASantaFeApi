using Microsoft.AspNetCore.Authorization;
using Application.DTO.Partial;
using Application.DTO.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Presentation.Security;



namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SedeController : ControllerBase
    {
        private readonly ISedeAppService _sedeAppService;
        private readonly IOutputCacheStore cache;
        private readonly CatalogCacheRevision revision;
        public SedeController(ISedeAppService sedeAppService,IOutputCacheStore cache,CatalogCacheRevision revision)
        {
            _sedeAppService = sedeAppService;
            this.cache=cache;
            this.revision=revision;
        }
        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        [EnableRateLimiting("escritura")]
        public async Task<IActionResult> AgregarSede([FromBody] SedeRequest sedeRequest, CancellationToken cancellationToken)
        {
            await _sedeAppService.AgregarSedeAsync(sedeRequest, cancellationToken);
            revision.Changed(PublicCatalogCache.SedesTag);
            await cache.EvictByTagAsync(PublicCatalogCache.SedesTag,cancellationToken);
            return Ok(new { Mensaje = "Sede agregada exitosamente." });
        }

        [AllowAnonymous]
        [HttpGet]
        [OutputCache(PolicyName=PublicCatalogCache.Sedes)]
        public async Task<IActionResult> ObtenerSedes(CancellationToken cancellationToken)
        {
            var sedes = await _sedeAppService.MostrarSedesAsync(cancellationToken);
            return Ok(sedes);
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpPatch("{id:guid}")]
        [EnableRateLimiting("escritura")]
        public async Task<IActionResult> ActualizarSede(Guid id, [FromBody] SedePartial sedePartial, CancellationToken cancellationToken)
        {
            await _sedeAppService.ActualizarSedeAsync(id, sedePartial, cancellationToken);
            revision.Changed(PublicCatalogCache.SedesTag);
            await cache.EvictByTagAsync(PublicCatalogCache.SedesTag,cancellationToken);
            return Ok(new { Mensaje = "Sede actualizada exitosamente." });
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpDelete("{id:guid}")]
        [EnableRateLimiting("escritura")]
        public async Task<IActionResult> EliminarSede(Guid id, CancellationToken cancellationToken)
        {
            await _sedeAppService.EliminarSedeAsync(id, cancellationToken);
            revision.Changed(PublicCatalogCache.SedesTag);
            await cache.EvictByTagAsync(PublicCatalogCache.SedesTag,cancellationToken);
            return Ok(new { Mensaje = "Sede eliminada exitosamente." });
        }
    }
}
