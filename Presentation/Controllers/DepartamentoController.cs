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
    public class DepartamentoController : ControllerBase
    {
        private readonly IDepartamentoAppService _departamentoAppService;
        private readonly IOutputCacheStore cache;
        private readonly CatalogCacheRevision revision;
        public DepartamentoController(IDepartamentoAppService departamentoAppService, IOutputCacheStore cache, CatalogCacheRevision revision)
        {
            _departamentoAppService = departamentoAppService;
            this.cache = cache;
            this.revision = revision;
        }

        [AllowAnonymous]
        [HttpGet]
        [OutputCache(PolicyName = PublicCatalogCache.Departments)]
        [ProducesResponseType(typeof(IEnumerable<DepartamentoResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerDepartamentos(CancellationToken cancellationToken)
        {
            return Ok(await _departamentoAppService.ObtenerDepartamentosAsync(cancellationToken));
        }

        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        [EnableRateLimiting("escritura")]
        public async Task<IActionResult> AgregarDepartamento([FromBody] string nombre, CancellationToken cancellationToken)
        {
            await _departamentoAppService.AgregarDepartamentoAsync(nombre, cancellationToken);
            revision.Changed(PublicCatalogCache.GeographyTag);
            await cache.EvictByTagAsync(PublicCatalogCache.GeographyTag, cancellationToken);
            return Ok(new { Mensaje = "Departamento agregado exitosamente." });
        }
    }
}
