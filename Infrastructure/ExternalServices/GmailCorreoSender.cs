using System.Net;
using System.Net.Mail;
using System.Text;
using Application.Interfaces;
using Domain.Models;
using Application.Authentication;
using System.Security.Cryptography;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
namespace Infrastructure.ExternalServices;
public sealed class GmailCorreoSender : ICorreoSender,IDisposable
{
    private readonly GmailService service;
    private readonly GoogleAuthorizationCodeFlow? flow;
    private readonly string sender;
    private readonly ICodigoCuentaService? codigos;
    private readonly CuentaSecuritySettings? cuenta;
    // Permite comprobar el protocolo con un transporte simulado, sin usar credenciales.
    public GmailCorreoSender(GmailOptions options,GmailService service,ICodigoCuentaService? codigos=null,CuentaSecuritySettings? cuenta=null)
    {
        sender=options.Sender;
        this.service=service;
        this.codigos=codigos;this.cuenta=cuenta;
    }
    public GmailCorreoSender(GmailOptions options,ICodigoCuentaService codigos,CuentaSecuritySettings cuenta)
    {
        sender=options.Sender;
        this.codigos=codigos;this.cuenta=cuenta;
        flow=new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets=new ClientSecrets {ClientId=options.ClientId,ClientSecret=options.ClientSecret},
            Scopes=[GmailService.Scope.GmailSend]
        });
        var credential=new UserCredential(flow,"sender",new TokenResponse {RefreshToken=options.RefreshToken});
        service=new(new BaseClientService.Initializer
        {
            HttpClientInitializer=credential,ApplicationName="LLASantaFeApi",
            DefaultExponentialBackOffPolicy=Google.Apis.Http.ExponentialBackOffPolicy.None
        });
        service.HttpClient.Timeout=TimeSpan.FromSeconds(20);
    }
    public static string CrearMime(CorreoPendiente correo,string sender,string? enlace=null)
    {
        if (!MailAddress.TryCreate(correo.Email,out var address) || address.Address!=correo.Email ||
            correo.Email.Contains('\r') || correo.Email.Contains('\n')) throw new FormatException("Destinatario inválido.");
        var (subject,body)=correo.Tipo switch
        {
            "bienvenida" => ("Registro recibido","Tu cuenta fue creada correctamente. Ya podés iniciar sesión en la aplicación. Si no realizaste este registro, podés responder a este correo para solicitar asistencia."),
            "afiliacion_aprobada" => ("Actualización de tu solicitud","Tu solicitud de afiliación fue aprobada en la aplicación. Podés consultar su estado desde tu cuenta. Este aviso confirma el estado registrado en el sistema."),
            "email_verificacion" when enlace is not null => ("Verificá tu email",$"Para confirmar tu registro, abrí este enlace e ingresá el email y la contraseña que elegiste al registrarte:\r\n{enlace}\r\nEl enlace vence y solo puede utilizarse una vez. Si no hiciste el registro, ignorá este correo."),
            "password_recuperacion" when enlace is not null => ("Recuperación de contraseña",$"Para elegir una nueva contraseña, abrí este enlace:\r\n{enlace}\r\nEl enlace vence y solo puede utilizarse una vez. Si no lo solicitaste, ignorá este correo; tu contraseña no fue modificada."),
            "password_modificada" => ("Contraseña actualizada","La contraseña de tu cuenta fue actualizada. Las sesiones anteriores fueron revocadas. Si no realizaste esta acción, solicitá asistencia o recuperá tu cuenta desde el sitio."),
            _ => throw new ArgumentException("Tipo de correo no admitido.")
        };
        return $"From: {sender}\r\nTo: {correo.Email}\r\nSubject: =?UTF-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=\r\n"+
            $"Message-ID: <{correo.Id:N}@{new MailAddress(sender).Host}>\r\nDate: {DateTimeOffset.UtcNow.ToString("r")}\r\n"+
            "MIME-Version: 1.0\r\nContent-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: base64\r\n\r\n"+
            Convert.ToBase64String(Encoding.UTF8.GetBytes(body),Base64FormattingOptions.InsertLineBreaks)+"\r\n";
    }
    public async Task<ResultadoCorreo> EnviarAsync(CorreoPendiente correo,CancellationToken ct)
    {
        try
        {
            string? enlace=null;
            if (correo.Tipo is "email_verificacion" or "password_recuperacion")
            {
                if (codigos is null || cuenta is null || correo.AccionId is null || correo.CodigoHash is null || correo.VenceAt is null || correo.VenceAt<=DateTime.UtcNow)
                    return new("cancelado",Error:"codigo_no_vigente");
                var codigo=codigos.Crear(correo.AccionId.Value,correo.Tipo);
                if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(correo.CodigoHash),Convert.FromHexString(codigos.Huella(codigo))))
                    return new("cancelado",Error:"clave_codigo_rotada");
                var path=correo.Tipo=="email_verificacion" ? "/verificar-email" : "/restablecer-password";
                enlace=$"{cuenta.FrontendUrl.TrimEnd('/')}{path}#codigo={Uri.EscapeDataString(codigo)}";
            }
            var raw=Convert.ToBase64String(Encoding.UTF8.GetBytes(CrearMime(correo,sender,enlace))).TrimEnd('=').Replace('+','-').Replace('/','_');
            var response=await service.Users.Messages.Send(new Message {Raw=raw},"me").ExecuteAsync(ct);
            return string.IsNullOrEmpty(response.Id) ? new("incierto",Error:"respuesta_sin_id") : new("enviado",response.Id);
        }
        catch (FormatException) { return new("fallido",Error:"destinatario_invalido"); }
        catch (TokenResponseException) { return new("fallido",Error:"oauth_rechazado"); }
        catch (GoogleApiException ex) when (ex.HttpStatusCode==HttpStatusCode.TooManyRequests ||
            (ex.HttpStatusCode==HttpStatusCode.Forbidden && ex.Error?.Errors?.Any(e=>e.Reason is "rateLimitExceeded" or "userRateLimitExceeded" or "dailyLimitExceeded")==true))
        { return new("pendiente",Error:"cuota_gmail"); }
        catch (GoogleApiException ex) when ((int)ex.HttpStatusCode is >=400 and <500)
        { return new("fallido",Error:"gmail_rechazado"); }
        // Timeouts, desconexiones y errores 5xx son ambiguos: no duplicamos automáticamente.
        catch (Exception) { return new("incierto",Error:"respuesta_no_confirmada"); }
    }
    public void Dispose() { service.Dispose();flow?.Dispose(); }
}
