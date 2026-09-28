using Application.Authentication;
namespace Infrastructure.Authentication;
public sealed class PasswordService : IPasswordService
{
    // Mismo costo cuando la cuenta no existe: evita el atajo de tiempo de BCrypt.
    private readonly string dummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 12);
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    public bool Verify(string password, string? hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash ?? dummyHash) && hash is not null; }
        catch (BCrypt.Net.SaltParseException) { BCrypt.Net.BCrypt.Verify(password, dummyHash); return false; }
    }
}
