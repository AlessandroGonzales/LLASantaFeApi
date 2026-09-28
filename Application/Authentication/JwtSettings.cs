namespace Application.Authentication;
// Las implementaciones criptográficas están en Infrastructure.Authentication.
public sealed class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public double DurationInMinutes { get; set; } = 15;
}
