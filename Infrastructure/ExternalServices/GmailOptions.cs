using System.Net.Mail;
namespace Infrastructure.ExternalServices;
public sealed class GmailOptions
{
    public bool Enabled { get; set; }
    public string Sender { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int DailyLimit { get; set; } = 100;
    public int IdleMinSeconds { get; set; } = 30;
    public int IdleMaxSeconds { get; set; } = 300;
    public void Validate()
    {
        if (IdleMinSeconds is < 10 or > 300 || IdleMaxSeconds < IdleMinSeconds || IdleMaxSeconds > 900)
            throw new InvalidOperationException("Gmail: espera mínima entre 10 y 300 segundos y máxima entre la mínima y 900.");
        if (!Enabled) return;
        if (!MailAddress.TryCreate(Sender,out var address) || address.Address!=Sender || Sender.Contains('\r') || Sender.Contains('\n') ||
            string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret) || string.IsNullOrWhiteSpace(RefreshToken) ||
            DailyLimit is <1 or >500)
            throw new InvalidOperationException("Configure Gmail: Sender, ClientId, ClientSecret, RefreshToken y DailyLimit entre 1 y 500.");
    }
}
