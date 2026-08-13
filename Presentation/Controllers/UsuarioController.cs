using Application.Authentication;
using Application.DTO.Partial;
using Application.DTO.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuarioController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly IUsuarioAppService _usuarioAppService;
        private readonly IRoleAppService _roleAppService;
        public UsuarioController(JwtService jwtService, IUsuarioAppService usuarioAppService, IRoleAppService roleAppService)
        {
            _jwtService = jwtService;
            _usuarioAppService = usuarioAppService;
            _roleAppService = roleAppService;
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user ID in token.");
            }
            return userId;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest login, CancellationToken cancellationToken)
        {
            var user = await _usuarioAppService.ValidarUsuarioAsync(login.Email, login.Password, cancellationToken);
            if(user == null)
            {
                return Unauthorized("Invalid email or password.");
            }
            
            var role = await _roleAppService.ObtenerRolIdPorNombreAsync(user.RoleNombre, cancellationToken);

            var token = _jwtService.GenerateToken(user, role);
            return Ok(new { Token = token });
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetPerfilUsuario(CancellationToken cancellationToken)
        {
            var userId = GetUserIdFromToken();
            if(userId == Guid.Empty) return Unauthorized("Invalid user ID.");

            var perfil = await _usuarioAppService.VerPerfilUsuarioAsync(userId, cancellationToken);
            return perfil != null ? Ok(perfil) : NotFound("User profile not found.");

        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> AgregarUsuario([FromBody] UsuarioRequest usuarioRequest, CancellationToken cancellationToken)
        {
            await _usuarioAppService.AgregarUsuarioAsync(usuarioRequest, cancellationToken);
            return Ok();
        }

        [HttpPatch("me")]
        public async Task<IActionResult> ActualizarUsuario( [FromBody] UsuarioPartial updatedUsuario, CancellationToken cancellationToken)
        {
            var userId = GetUserIdFromToken();
            if(userId == Guid.Empty) return Unauthorized("Invalid user ID.");
            await _usuarioAppService.ActualizarUsuarioAsync(userId, updatedUsuario, cancellationToken);
            return Ok();
        }

        [HttpDelete("me")]
        public async Task<IActionResult> DesactivarUsuario(CancellationToken cancellationToken)
        {
            var userId = GetUserIdFromToken();
            if(userId == Guid.Empty) return Unauthorized("Invalid user ID.");
            await _usuarioAppService.DesactivarUsuarioAsync(userId, cancellationToken);
            return Ok();
        }
    }
}
