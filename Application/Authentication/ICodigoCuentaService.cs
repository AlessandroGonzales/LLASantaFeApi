namespace Application.Authentication;

public interface ICodigoCuentaService
{
    string Crear(Guid id,string tipo);
    string Huella(string codigo);
    bool Leer(string codigo,out Guid id,out string huella);
}

public sealed class CuentaSecuritySettings
{
    public string FrontendUrl { get; set; } = "http://localhost:5173";
    public int VerificacionMinutes { get; set; } = 60;
    public int RecuperacionMinutes { get; set; } = 30;
    public void Validate()
    {
        if (!Uri.TryCreate(FrontendUrl,UriKind.Absolute,out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath!="/" ||
            (uri.Scheme!="https" && !(uri.Scheme=="http" && (uri.Host=="localhost" || uri.Host=="127.0.0.1"))) ||
            VerificacionMinutes is <5 or >120 || RecuperacionMinutes is <5 or >60)
            throw new InvalidOperationException("Configure FrontendUrl como origen HTTPS (HTTP solo localhost) y CuentaSecurity: duración válida de códigos.");
    }
}
