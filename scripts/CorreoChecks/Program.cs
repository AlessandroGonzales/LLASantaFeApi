using System.Net;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.Services;
using Domain.Models;
using Domain.Repositories;
using Google.Apis.Gmail.v1;
using Google.Apis.Http;
using Google.Apis.Services;
using Infrastructure.ExternalServices;
using Infrastructure.Repositories;
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

// Prueba opcional del SQL de finalización: exige la base desechable de la suite.
var cs=Environment.GetEnvironmentVariable("CORREO_TEST_CONNECTION");
if(cs is not null)
{
    var builder=new NpgsqlConnectionStringBuilder(cs);
    if(builder.Host!="127.0.0.1" || builder.Port!=55432 || !builder.Database!.StartsWith("net10_usuario_"))
        throw new Exception("Solo se admite la base temporal de la suite.");
    await using var source=NpgsqlDataSource.Create(cs);
    await using var choose=source.CreateCommand("UPDATE public.correos_transaccionales SET estado='procesando' WHERE id=(SELECT id FROM public.correos_transaccionales LIMIT 1) RETURNING id");
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
