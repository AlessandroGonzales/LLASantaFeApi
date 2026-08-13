using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Application.DTO.Response;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Application.Authentication
{
    public class JwtSettings
    {
        public string Key { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public double DurationInMinutes { get; set; }
    }

    public class JwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<JwtService> _logger;
        private readonly byte[] _keyBytes;

        public JwtService(IOptions<JwtSettings> jwtOptions, ILogger<JwtService> logger)
        {
            _jwtSettings = jwtOptions.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_jwtSettings.Key))
                throw new InvalidOperationException("Falta la clave JWT en la configuración.");

            _keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);
        }

        public string GenerateToken(UsuarioResponse usuario, RoleResponse role)
        {
            _logger?.LogInformation("=== JWT Generate: Usuario={UsuarioId}, Role={Role}", usuario.IdUsuario, role.RoleNombre);

            var key = new SymmetricSecurityKey(_keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new Dictionary<string, object>
            {
                { JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString() },
                { JwtRegisteredClaimNames.Name, usuario.Nombre },
                { ClaimTypes.Role, role.RoleNombre },
                { "imageUrl", usuario.FotoPerfilUrl ?? string.Empty }
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                Claims = claims,
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                SigningCredentials = creds
            };

            var handler = new JsonWebTokenHandler();
            return handler.CreateToken(descriptor);
        }
    }
}