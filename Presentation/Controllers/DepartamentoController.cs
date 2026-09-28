using Microsoft.AspNetCore.Authorization;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DepartamentoController : ControllerBase
    {
        private readonly IDepartamentoAppService _departamentoAppService;
        public DepartamentoController(IDepartamentoAppService departamentoAppService)
        {
            _departamentoAppService = departamentoAppService;
        }


        [Authorize(Policy = "AdminPolicy")]
        [HttpPost]
        public async Task<IActionResult> AgregarDepartamento([FromBody] string nombre, CancellationToken cancellationToken)
        {
            await _departamentoAppService.AgregarDepartamentoAsync(nombre, cancellationToken);
            return Ok(new { Mensaje = "Departamento agregado exitosamente." });
        }
    }
}
