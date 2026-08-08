using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens; // Novedad: El handler moderno y rápido
using Microsoft.IdentityModel.Tokens;
using Application.DTO.Response;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Application.Authentication
{
    // Clase fuertemente tipada para mapear el appsettings.json
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

        // Se inyecta la configuración ya cacheada en memoria (Patrón Options)
        public JwtService(IOptions<JwtSettings> jwtOptions, ILogger<JwtService> logger)
        {
            _jwtSettings = jwtOptions.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_jwtSettings.Key))
                throw new InvalidOperationException("Falta la clave JWT en la configuración.");

            // OPTIMIZACIÓN: Los bytes de la clave se calculan 1 sola vez cuando arranca la API, no en cada login.
            _keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.Key);
        }

        // Asumiendo que tus DTOs se llaman UsuarioResponse y RoleResponse
        public string GenerateToken(UsuarioResponse usuario, RoleResponse role)
        {
            _logger?.LogInformation("=== JWT Generate: Usuario={UsuarioId}, Role={Role}", usuario.IdUsuario, role.RoleName);

            var key = new SymmetricSecurityKey(_keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // OPTIMIZACIÓN: Usar un Dictionary en lugar de un arreglo de new Claim() reduce la asignación de memoria (Garbage Collector)
            var claims = new Dictionary<string, object>
            {
                { JwtRegisteredClaimNames.Sub, usuario.IdUsuario.ToString() },
                { JwtRegisteredClaimNames.Name, usuario.UserName },
                { ClaimTypes.Role, role.RoleName },
                { "imageUrl", usuario.ImageUrl ?? string.Empty }
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                Claims = claims,
                // OPTIMIZACIÓN: Siempre usar DateTime.UtcNow para tokens, evita bugs de zonas horarias.
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                SigningCredentials = creds
            };

            // OPTIMIZACIÓN: JsonWebTokenHandler es un 30% más rápido que el antiguo JwtSecurityTokenHandler en .NET 8.
            var handler = new JsonWebTokenHandler();
            return handler.CreateToken(descriptor);
        }
    }
}