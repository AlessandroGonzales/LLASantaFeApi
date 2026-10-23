using System.Security.Cryptography;
using System.Text;
using Application.Authentication;
using Microsoft.Extensions.Options;

namespace Infrastructure.Authentication;

public sealed class CodigoCuentaService : ICodigoCuentaService
{
    private readonly byte[] key;
    public CodigoCuentaService(IOptions<JwtSettings> settings)
    {
        var secret=settings.Value.Key;
        if (Encoding.UTF8.GetByteCount(secret)<32) throw new InvalidOperationException("Falta la clave de seguridad de cuenta.");
        // Derivación por propósito: los códigos no son JWT y no se almacena el secreto en DB.
        key=HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),"LLASantaFe:cuenta:v1"u8);
    }
    public string Crear(Guid id,string tipo)
    {
        if (tipo is not ("email_verificacion" or "password_recuperacion")) throw new ArgumentException("Tipo inválido.");
        var token=Convert.ToBase64String(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes($"{tipo}:{id:D}")))
            .TrimEnd('=').Replace('+','-').Replace('/','_');
        return $"{id:D}.{token}";
    }
    public string Huella(string codigo) => Leer(codigo,out _,out var hash) ? hash : throw new ArgumentException("Código inválido.");
    public bool Leer(string codigo,out Guid id,out string huella)
    {
        id=default;huella="";
        if (codigo.Length!=80 || codigo[36]!='.' || !Guid.TryParseExact(codigo.AsSpan(0,36),"D",out id) || id==Guid.Empty) return false;
        var token=codigo.AsSpan(37);
        foreach (var c in token) if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return false;
        huella=Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token.ToString())));
        return true;
    }
}
