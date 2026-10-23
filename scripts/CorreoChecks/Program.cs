using System.Net;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.Authentication;
using Application.Services;
using Domain.Models;
using Domain.Repositories;
using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Infrastructure.ExternalServices;
using Infrastructure.Repositories;
using Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Npgsql;

int checks=0;
void Check(bool ok,string label) { if(!ok) throw new Exception(label); checks++; }
var correo=new CorreoPendiente(Guid.NewGuid(),"bienvenida","test@example.invalid",1);
var options=new GmailOptions {Sender="sender@example.invalid"};
foreach(var sample in new[] {
    (200,"{\"id\":\"gmail-test-id\"}","enviado"),
    (429,"{\"error\":{\"code\":429,\"message\":\"Quota\"}}","pendiente"),
    (403,"{\"error\":{\"code\":403,\"errors\":[{\"reason\":\"userRateLimitExceeded\"}]}}","pendiente"),
    (403,"{\"error\":{\"code\":403,\"message\":\"Forbidden\"}}","fallido"),
    (500,"{\"error\":{\"code\":500,\"message\":\"Server\"}}","incierto") })
{
    var handler=new FakeHandler(sample.Item1,sample.Item2);
    using var gmail=new GmailCorreoSender(options,new GmailService(new BaseClientService.Initializer {
        HttpClientFactory=new FakeFactory(handler),DefaultExponentialBackOffPolicy=ExponentialBackOffPolicy.None }));
    var result=await gmail.EnviarAsync(correo,CancellationToken.None);
    Check(result.Estado==sample.Item3,"Estado ante respuesta Gmail");
    Check(handler.Count==1,"El SDK reintentó automáticamente el envío");
    using var json=JsonDocument.Parse(handler.Body!);
    var raw=json.RootElement.GetProperty("raw").GetString()!.Replace('-','+').Replace('_','/');
    var mime=Encoding.UTF8.GetString(Convert.FromBase64String(raw.PadRight((raw.Length+3)/4*4,'=')));
    Check(mime.Contains("To: test@example.invalid\r\n") && mime.Contains($"Message-ID: <{correo.Id:N}@example.invalid>"),"MIME o destinatario incorrectos");
}
try { GmailCorreoSender.CrearMime(correo with {Email="x@example.invalid\r\nBcc: victim@example.invalid"},options.Sender); throw new Exception("Aceptó inyección"); }
catch(FormatException) { checks++; }
foreach(var state in new[]{"enviado","pendiente","fallido","incierto"})
{
    var repo=new FakeRepo(correo);
    await new CorreoDispatcher(repo,new FakeSender(new(state))).ProcesarUnoAsync(100,CancellationToken.None);
    Check(repo.Estado==state,"Dispatcher altera resultado");
}
var exhausted=new FakeRepo(correo with {Intentos=5});
await new CorreoDispatcher(exhausted,new FakeSender(new("pendiente"))).ProcesarUnoAsync(100,CancellationToken.None);
Check(exhausted.Estado=="fallido","Reintentos ilimitados");
var interrupted=new FakeRepo(correo);
await new CorreoDispatcher(interrupted,new FakeSender(null)).ProcesarUnoAsync(100,CancellationToken.None);
Check(interrupted.Estado=="incierto","Pérdida de estado ante desconexión");

var security=Options.Create(new JwtSettings {Key=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))});
var codes=new CodigoCuentaService(security);
var actionId=Guid.NewGuid();
var verifyCode=codes.Crear(actionId,"email_verificacion");
var resetCode=codes.Crear(actionId,"password_recuperacion");
Check(verifyCode.Length==80 && verifyCode!=resetCode,"Código no está ligado al propósito");
Check(codes.Leer(verifyCode,out var parsed,out var hash) && parsed==actionId && hash.Length==64,"Formato/huella de código incorrectos");
Check(!codes.Leer("malformed",out _,out _) && !codes.Leer(verifyCode[..^1]+"!",out _,out _),"Aceptó código mal formado");
var rotated=new CodigoCuentaService(Options.Create(new JwtSettings {Key=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))}));
Check(rotated.Crear(actionId,"email_verificacion")!=verifyCode,"Rotación de clave no invalida código");
var accountSettings=new CuentaSecuritySettings {FrontendUrl="https://frontend.example.invalid"};accountSettings.Validate();
try {new CuentaSecuritySettings {FrontendUrl="https://frontend.example.invalid/?redirect=evil"}.Validate();throw new Exception("Admite origen manipulable");}
catch(InvalidOperationException) {checks++;}
foreach(var kind in new[]{"email_verificacion","password_recuperacion"})
{
    var code=codes.Crear(actionId,kind);
    var secured=correo with {Tipo=kind,AccionId=actionId,CodigoHash=codes.Huella(code),VenceAt=DateTime.UtcNow.AddMinutes(30)};
    var transport=new FakeHandler(200,"{\"id\":\"account-test-id\"}");
    using var gmail=new GmailCorreoSender(options,new GmailService(new BaseClientService.Initializer {
        HttpClientFactory=new FakeFactory(transport),DefaultExponentialBackOffPolicy=ExponentialBackOffPolicy.None }),codes,accountSettings);
    Check((await gmail.EnviarAsync(secured,CancellationToken.None)).Estado=="enviado" && transport.Count==1,"No envía el código de cuenta");
    using var rawJson=JsonDocument.Parse(transport.Body!);
    var raw=rawJson.RootElement.GetProperty("raw").GetString()!.Replace('-','+').Replace('_','/');
    var mime=Encoding.UTF8.GetString(Convert.FromBase64String(raw.PadRight((raw.Length+3)/4*4,'=')));
    var text=Encoding.UTF8.GetString(Convert.FromBase64String(mime.Split("\r\n\r\n")[1]));
    Check(text.Contains("https://frontend.example.invalid/") && text.Contains("#codigo="+code),"Enlace no usa origen fijo/fragmento");
    Check(!text.Contains("?codigo=") && !text.Contains("Synthetic-Only-Password"),"Expone código en query o contraseña en correo");
    Check((await gmail.EnviarAsync(secured with {VenceAt=DateTime.UtcNow.AddSeconds(-1)},CancellationToken.None)).Estado=="cancelado" && transport.Count==1,"Envía código vencido");
    Check((await gmail.EnviarAsync(secured with {CodigoHash=new string('0',64)},CancellationToken.None)).Estado=="cancelado" && transport.Count==1,"Envía con clave no vigente");
}
Check(GmailCorreoSender.CrearMime(correo with {Tipo="password_modificada"},options.Sender).Contains("Content-Type: text/plain"),"Aviso de contraseña no está en texto plano");

// Prueba opcional del SQL de finalización: exige la base desechable de la suite.
var cs=Environment.GetEnvironmentVariable("CORREO_TEST_CONNECTION");
if(cs is not null)
{
    var builder=new NpgsqlConnectionStringBuilder(cs);
    if(builder.Host!="127.0.0.1" || builder.Port!=55432 || !builder.Database!.StartsWith("net10_usuario_"))
        throw new Exception("Solo se admite la base temporal de la suite.");
    await using var source=NpgsqlDataSource.Create(cs);
    await using var choose=source.CreateCommand("UPDATE public.correos_transaccionales SET estado='procesando' WHERE id=(SELECT c.id FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id WHERE c.tipo='bienvenida' AND u.activo AND u.deleted_at IS NULL LIMIT 1) RETURNING id");
    var id=(Guid)(await choose.ExecuteScalarAsync())!;
    var repository=new NotificacionRepository(source);
    await repository.FinalizarAsync(id,"enviado","fake-provider-id",null,0,CancellationToken.None);
    await using var state=source.CreateCommand("SELECT estado||':'||proveedor_id FROM public.correos_transaccionales WHERE id=$1");
    state.Parameters.AddWithValue(id);
    Check((string)(await state.ExecuteScalarAsync())! == "enviado:fake-provider-id","Finalización SQL incorrecta");
    await repository.FinalizarAsync(id,"fallido",null,"late",0,CancellationToken.None);
    Check((string)(await state.ExecuteScalarAsync())! == "enviado:fake-provider-id","Resultado tardío sobrescribe envío confirmado");
    async Task Prepare()
    {
        foreach(var sql in new[] {
            "UPDATE public.correos_transaccionales SET disponible_at=now()+interval '1 day'",
            "UPDATE public.correos_transaccionales SET estado='pendiente',intentos=0,disponible_at=now() WHERE id=$1",
            "UPDATE public.correo_presupuesto SET intentos=0,dia=(now() AT TIME ZONE 'UTC')::date,siguiente_at=now()" })
        {
            await using var prepare=source.CreateCommand(sql);
            if(sql.Contains("$1")) prepare.Parameters.AddWithValue(id);
            await prepare.ExecuteNonQueryAsync();
        }
    }
    await Prepare();
    var claims=await Task.WhenAll(repository.ReservarAsync(100,CancellationToken.None),repository.ReservarAsync(100,CancellationToken.None));
    Check(claims.Count(c=>c is not null)==1,"Dos workers reservaron el mismo correo");
    await Prepare();
    await new CorreoDispatcher(repository,new FakeSender(new("enviado","fake-provider-id"))).ProcesarUnoAsync(100,CancellationToken.None);
    Check((string)(await state.ExecuteScalarAsync())! == "enviado:fake-provider-id","Dispatcher y repositorio no completaron el envío simulado");

    string targetEmail;
    Guid targetUser;
    await using (var target=source.CreateCommand("SELECT id,email FROM public.usuarios WHERE activo AND deleted_at IS NULL AND email_verificado_at IS NOT NULL LIMIT 1"))
    await using (var reader=await target.ExecuteReaderAsync())
    {if(!await reader.ReadAsync()) throw new Exception("Falta usuario sintético");targetUser=reader.GetGuid(0);targetEmail=reader.GetString(1);}
    foreach(var sql in new[]{"DELETE FROM public.usuario_acciones WHERE usuario_id=$1 AND tipo='password_recuperacion'",
        "UPDATE public.correos_transaccionales SET disponible_at=now()+interval '1 day'",
        "UPDATE public.correo_presupuesto SET siguiente_at=now(),intentos=0"})
    {
        await using var prepare=source.CreateCommand(sql);
        if(sql.Contains("$1")) prepare.Parameters.AddWithValue(targetUser);
        await prepare.ExecuteNonQueryAsync();
    }
    var sqlAction=Guid.NewGuid();var sqlCode=codes.Crear(sqlAction,"password_recuperacion");
    await using (var issue=source.CreateCommand("SELECT public.solicitar_accion_usuario($1,'password_recuperacion',$2,$3,30)"))
    {issue.Parameters.AddWithValue(targetEmail);issue.Parameters.AddWithValue(sqlAction);issue.Parameters.AddWithValue(codes.Huella(sqlCode));Check((bool)(await issue.ExecuteScalarAsync())!,"No encola acción SQL");}
    var pending=await repository.ReservarAsync(100,CancellationToken.None);
    Check(pending is not null && pending.AccionId==sqlAction && pending.CodigoHash==codes.Huella(sqlCode) && pending.VenceAt>DateTime.UtcNow,"Lectura de metadata SQL incorrecta");
    var handler=new FakeHandler(200,"{\"id\":\"sql-account-test\"}");
    using var accountGmail=new GmailCorreoSender(options,new GmailService(new BaseClientService.Initializer {HttpClientFactory=new FakeFactory(handler)}),codes,accountSettings);
    var sqlResult=await accountGmail.EnviarAsync(pending!,CancellationToken.None);
    await repository.FinalizarAsync(pending!.Id,sqlResult.Estado,sqlResult.ProveedorId,sqlResult.Error,0,CancellationToken.None);
    Check(sqlResult.Estado=="enviado" && handler.Count==1,"SQL y Gmail simulado no completan código de cuenta");
}
Console.WriteLine($"OK: {checks} comprobaciones de transporte simulado y dispatcher; cero correos reales.");

sealed class FakeHandler(int status,string response) : HttpMessageHandler
{
    public int Count; public string? Body;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        Count++;
        await using var stream=await request.Content!.ReadAsStreamAsync(ct);
        if(request.Content.Headers.ContentEncoding.Contains("gzip"))
        {
            await using var gzip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress);
            using var reader=new StreamReader(gzip);Body=await reader.ReadToEndAsync(ct);
        }
        else { using var reader=new StreamReader(stream);Body=await reader.ReadToEndAsync(ct); }
        return new((HttpStatusCode)status) {Content=new StringContent(response,Encoding.UTF8,"application/json")};
    }
}
sealed class FakeFactory(FakeHandler handler) : Google.Apis.Http.IHttpClientFactory
{
    public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)=>new(new ConfigurableMessageHandler(handler));
}
sealed class FakeSender(ResultadoCorreo? result) : ICorreoSender
{
    public Task<ResultadoCorreo> EnviarAsync(CorreoPendiente c,CancellationToken ct) => result is null
        ? throw new HttpRequestException("Simulated interruption") : Task.FromResult(result);
}
sealed class FakeRepo(CorreoPendiente correo) : INotificacionRepository
{
    public string? Estado;
    public Task<NotificacionEstado?> EncolarAsync(string t,Guid r,CancellationToken ct)=>throw new NotSupportedException();
    public Task<CorreoPendiente?> ReservarAsync(int limite,CancellationToken ct)=>Task.FromResult<CorreoPendiente?>(correo);
    public Task FinalizarAsync(Guid id,string estado,string? proveedor,string? error,int espera,CancellationToken ct)
    {Estado=estado;return Task.CompletedTask;}
}
