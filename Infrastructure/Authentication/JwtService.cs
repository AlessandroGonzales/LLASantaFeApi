using Application.Authentication;
using Application.DTO.Response;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;
namespace Infrastructure.Authentication;
public sealed class JwtService(IOptions<JwtSettings> options, TimeProvider clock) : ITokenService
{
    public LoginResponse Create(Usuario usuario)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.DurationInMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer, Audience = settings.Audience,
            IssuedAt = now, NotBefore = now, Expires = expires,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = usuario.Id.ToString(),
                ["role"] = usuario.RoleNombre ?? throw new InvalidOperationException("Falta el rol."),
                ["ver"] = usuario.VersionAcceso.ToString(),
                ["jti"] = Guid.NewGuid().ToString()
            }
        };
        return new(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
