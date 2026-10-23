using System.ComponentModel.DataAnnotations;
using System.Net;
using Application.Interfaces;
using Application.Services;
using Application.DTO.Request;
using Domain.Models;
using Domain.Repositories;
using Domain.Exceptions;
using System.Text.Json;
using Infrastructure.ExternalServices;
using Infrastructure.DependencyInjection;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Presentation.Security;

int checks=0;
void Check(bool ok,string message) { if (!ok) throw new Exception(message); checks++; }
var surveyRepo=new RecordingEncuestaRepository();
var surveyService=new EncuestaAppService(surveyRepo,TimeProvider.System);
var surveyEdit=new EncuestaEditarRequest {Titulo="Consulta sobre el sitio",Revision=1,
    Configuracion=JsonSerializer.Deserialize<JsonElement>("""{"version":1,"preguntas":[{"id":"comentario","texto":"Comentario","tipo":"texto"}]}""")};
await surveyService.EditarAsync(Guid.NewGuid(),surveyEdit,CancellationToken.None);
Check(surveyRepo.DetailReads==0 && surveyRepo.ExistenceReads==0 && surveyRepo.Writes==1,"Editar un borrador lee su configuración anterior innecesariamente");
await surveyService.CambiarEstadoAsync(Guid.NewGuid(),new EncuestaEstadoRequest {Revision=1},false,CancellationToken.None);
Check(surveyRepo.DetailReads==0 && surveyRepo.ExistenceReads==0 && surveyRepo.Writes==2,"Cerrar una encuesta carga preguntas innecesarias");
await surveyService.RespuestasAsync(Guid.NewGuid(),null,10,CancellationToken.None);
Check(surveyRepo.DetailReads==0 && surveyRepo.ExistenceReads==1 && surveyRepo.ResponseReads==1,"Consultar resultados carga la configuración completa");
var affiliationRepo=new RecordingAfiliacionRepository();
var affiliationWake=new RecordingWakeSignal();
var affiliationService=new SolicitudesAfiliacionAppService(affiliationRepo,affiliationWake);
await affiliationService.AprobarAsync(Guid.NewGuid(),Guid.NewGuid(),new AfiliacionAprobarRequest(),CancellationToken.None);
Check(affiliationRepo.Writes==1 && affiliationRepo.ExistenceReads==0 && affiliationWake.Count==1,
    "La aprobación carga contactos innecesarios o no despierta el correo tras confirmar");
await affiliationService.RechazarAsync(Guid.NewGuid(),Guid.NewGuid(),new AfiliacionRechazarRequest {Motivo="La solicitud no cumple las condiciones informadas."},CancellationToken.None);
Check(affiliationRepo.Writes==2 && affiliationRepo.ExistenceReads==0 && affiliationWake.Count==1,
    "El rechazo agrega consultas o despierta un worker sin correo pendiente");
try
{
    await affiliationService.RechazarAsync(Guid.NewGuid(),Guid.NewGuid(),new AfiliacionRechazarRequest {Motivo=new string(' ',20)},CancellationToken.None);
    throw new Exception("Aceptó un motivo vacío al normalizar");
}
catch (Exception ex) when (ex is ValidationException || ex is BusinessException {Code:"validation"}) { checks++; }
Check(affiliationRepo.Writes==2,"Un motivo inválido ejecutó una escritura");
affiliationRepo.Result=false;affiliationRepo.Exists=false;
try
{
    await affiliationService.AprobarAsync(Guid.NewGuid(),Guid.NewGuid(),new AfiliacionAprobarRequest(),CancellationToken.None);
    throw new Exception("La solicitud ausente no devuelve not_found");
}
catch (BusinessException ex) when (ex.Code=="not_found") { checks++; }
affiliationRepo.Exists=true;
try
{
    await affiliationService.RechazarAsync(Guid.NewGuid(),Guid.NewGuid(),new AfiliacionRechazarRequest {Motivo="La solicitud ya fue resuelta anteriormente."},CancellationToken.None);
    throw new Exception("La solicitud resuelta no devuelve conflict");
}
catch (BusinessException ex) when (ex.Code=="conflict") { checks++; }
Check(affiliationRepo.ExistenceReads==2 && affiliationWake.Count==1,"Una resolución fallida consulta contactos o notifica correo");
var errorContext=new DefaultHttpContext {RequestServices=new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider()};
await using (var errorBody=new MemoryStream())
{
    errorContext.Response.Body=errorBody;
    await new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance).TryHandleAsync(errorContext,
        new BusinessException("email_not_verified","Verificá tu email."),CancellationToken.None);
    errorBody.Position=0;
    using var problem=await JsonDocument.ParseAsync(errorBody);
    Check(errorContext.Response.StatusCode==403 && problem.RootElement.GetProperty("code").GetString()=="email_not_verified","Error de cuenta no publica code para React");
}
var dbOptions=new DatabaseConsumptionOptions();
var apiPool=new NpgsqlConnectionStringBuilder(dbOptions.ConnectionString("Host=example.invalid;Database=test;Maximum Pool Size=100;Minimum Pool Size=10",false));
var mailPool=new NpgsqlConnectionStringBuilder(dbOptions.ConnectionString("Host=example.invalid;Database=test",true));
Check(apiPool.MaxPoolSize==20 && apiPool.MinPoolSize==0 && mailPool.MaxPoolSize==2,"Presupuesto de conexiones no aplicado");
Check(apiPool.Timeout==5 && apiPool.CommandTimeout==15 && dbOptions.RetryCount==0,"Timeouts o reintentos incorrectos");
var disabledServices=new ServiceCollection();
disabledServices.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
{["ConnectionStrings:DefaultConnection"]="Host=example.invalid;Database=test",["Gmail:Enabled"]="false"}).Build());
Check(!disabledServices.Any(d=>d.ServiceType==typeof(Microsoft.Extensions.Hosting.IHostedService)),"Gmail deshabilitado registra un worker");
try { new DatabaseConsumptionOptions {ApiMaxPoolSize=0}.Validate(); throw new Exception("Aceptó pool inválido"); }
catch (ValidationException) { checks++; }
try { new GmailOptions {IdleMinSeconds=30,IdleMaxSeconds=10}.Validate(); throw new Exception("Aceptó espera inválida"); }
catch (InvalidOperationException) { checks++; }
using (var wake=new CorreoWakeSignal())
{
    wake.Notify();wake.Notify();wake.Notify();
    Check(await wake.WaitAsync(TimeSpan.Zero,CancellationToken.None),"Perdió señal anterior a la espera");
    Check(!await wake.WaitAsync(TimeSpan.Zero,CancellationToken.None),"Acumuló señales en lugar de agruparlas");
    var waiting=wake.WaitAsync(TimeSpan.FromSeconds(2),CancellationToken.None);
    wake.Notify();Check(await waiting,"No despertó durante la espera");
    using var cancel=new CancellationTokenSource();cancel.Cancel();
    try { await wake.WaitAsync(TimeSpan.FromSeconds(2),cancel.Token);throw new Exception("No canceló la espera"); }
    catch (OperationCanceledException) { checks++; }
    wake.Notify();Check(await wake.WaitAsync(TimeSpan.Zero,CancellationToken.None),"La cancelación consumió una señal futura");
}

foreach (var scenario in new[] {"empty","wake","error","processed"})
{
    using var cancel=new CancellationTokenSource();
    var repo=new WorkerRepository(scenario,cancel);
    var signal=new ScriptedSignal(scenario,cancel);
    var clock=new FastClock();
    var services=new ServiceCollection();
    services.AddSingleton<INotificacionRepository>(repo);
    services.AddSingleton<ICorreoSender>(new NoNetworkSender());
    services.AddScoped<CorreoDispatcher>();
    await using var provider=services.BuildServiceProvider();
    using var worker=new CorreoWorker(provider.GetRequiredService<IServiceScopeFactory>(),new GmailOptions(),
        NullLogger<CorreoWorker>.Instance,signal,clock);
    await worker.StartAsync(cancel.Token);
    await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
    await worker.StopAsync(CancellationToken.None);
    if (scenario=="empty") Check(signal.Waits.SequenceEqual(new[]{30d,60d,120d,240d,300d,300d}),"Sondeo vacío no reduce frecuencia o supera el techo");
    if (scenario=="wake") Check(signal.Waits.Take(4).SequenceEqual(new[]{30d,60d,30d,60d}),"La señal no reinicia la espera");
    if (scenario=="error") Check(clock.Delays.Take(6).SequenceEqual(new[]{30d,60d,120d,240d,300d,300d}),"Fallo de DB genera consultas rápidas");
    if (scenario=="processed") Check(repo.Finalized && repo.Calls>=2 && signal.Waits[0]==30,"No continúa vaciando la cola tras enviar");
    Check(worker.ExecuteTask.IsCompleted,"El worker no finalizó al cancelar");
}

var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
{
    ["RateLimits:General"]="10000", ["RateLimits:ReadConcurrency"]="1",
    ["RateLimits:WriteConcurrency"]="1", ["RateLimits:AuthenticationConcurrency"]="1",
    ["RateLimits:PdfConcurrency"]="1", ["RateLimits:Login"]="2",
    ["CatalogCache:CitiesSeconds"]="1", ["CatalogCache:MaxBodyBytes"]="1024",
    ["CatalogCache:DepartmentsSeconds"]="1"
}).Build();
try
{
    new ServiceCollection().AddApiRateLimits(new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string,string?> {["DatabaseConsumption:ApiMaxPoolSize"]="1"}).Build());
    throw new Exception("Aceptó concurrencia mayor al pool");
}
catch (InvalidOperationException) { checks++; }
var builder=WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddApiRateLimits(config);
builder.Services.AddPublicCatalogCache(config);
await using var app=builder.Build();
app.UseRouting();
app.UseRateLimiter();
app.Use(async (http,next) =>
{
    http.Response.OnStarting(() =>
    {
        if (!http.Response.Headers.ContainsKey("Cache-Control") || http.Response.StatusCode!=200 || http.Response.Headers.ContainsKey("Set-Cookie"))
            http.Response.Headers.CacheControl="no-store";
        return Task.CompletedTask;
    });
    await next();
});
app.UseOutputCache();
int hits=0;
int departmentHits=0;
app.MapGet("/departments",()=>Results.Json(new {hit=Interlocked.Increment(ref departmentHits)}))
    .AllowAnonymous().CacheOutput(PublicCatalogCache.Departments);
app.MapGet("/cities",(HttpContext h)=>Results.Json(new {hit=Interlocked.Increment(ref hits),department=h.Request.Query["departamentoId"].ToString(),origin=h.Request.Headers.Origin.ToString()}))
    .AllowAnonymous().CacheOutput(PublicCatalogCache.Cities);
app.MapGet("/error",()=> { Interlocked.Increment(ref hits);return Results.StatusCode(503); })
    .AllowAnonymous().CacheOutput(PublicCatalogCache.Sedes);
app.MapGet("/large",()=> { Interlocked.Increment(ref hits);return Results.Text(new string('x',2048)); })
    .AllowAnonymous().CacheOutput(PublicCatalogCache.Sedes);
app.MapGet("/cookie",(HttpContext h)=> { Interlocked.Increment(ref hits);h.Response.Cookies.Append("session","test");return Results.Text("cookie"); })
    .AllowAnonymous().CacheOutput(PublicCatalogCache.Sedes);
app.MapGet("/private",()=> { Interlocked.Increment(ref hits);return Results.Text("private"); }).CacheOutput(PublicCatalogCache.Sedes);
var raceEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var raceRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
int raceValue=0,raceCalls=0;
app.MapGet("/race",async ()=>
{
    var value=raceValue;
    if (Interlocked.Increment(ref raceCalls)==1) { raceEntered.SetResult();await raceRelease.Task; }
    return Results.Text(value.ToString());
}).AllowAnonymous().CacheOutput(PublicCatalogCache.Sedes);
app.MapPost("/evict",async (IOutputCacheStore store,CatalogCacheRevision revision)=>
{
    revision.Changed(PublicCatalogCache.GeographyTag);revision.Changed(PublicCatalogCache.SedesTag);
    await store.EvictByTagAsync(PublicCatalogCache.GeographyTag,CancellationToken.None);
    await store.EvictByTagAsync(PublicCatalogCache.SedesTag,CancellationToken.None);
    return Results.NoContent();
});
var held=new Dictionary<ApiWorkload,(TaskCompletionSource Entered,TaskCompletionSource Release)>();
foreach (var kind in Enum.GetValues<ApiWorkload>())
{
    var gate=(new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    held.Add(kind,gate);
    app.MapGet($"/hold/{kind}",async ()=> {gate.Item1.TrySetResult();await gate.Item2.Task;return Results.Ok();})
        .WithMetadata(new ApiWorkloadAttribute(kind));
}
app.MapPost("/login-throttle",()=>Results.Ok()).WithMetadata(new ApiWorkloadAttribute(ApiWorkload.Authentication)).RequireRateLimiting("login");
await app.StartAsync();
using var client=new HttpClient(new HttpClientHandler {UseCookies=false})
{BaseAddress=new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),Timeout=TimeSpan.FromSeconds(5)};
var department=Guid.NewGuid().ToString();var path=$"/cities?departamentoId={department}";
async Task<(HttpStatusCode Status,string Body,string Cache)> Get(string url,string? header=null,string? value=null)
{
    using var request=new HttpRequestMessage(HttpMethod.Get,url);
    if (header is not null) request.Headers.TryAddWithoutValidation(header,value);
    using var response=await client.SendAsync(request);
    return (response.StatusCode,await response.Content.ReadAsStringAsync(),response.Headers.CacheControl?.ToString() ?? "");
}
try
{
    var first=await Get(path);var second=await Get(path);
    Check(first.Status==HttpStatusCode.OK && first.Body==second.Body && hits==1,"Una lectura repetida volvió al origen");
    Check(first.Cache.Contains("public"),"Catálogo anónimo no tiene política pública");
    using (var response=await client.GetAsync(path))
        Check(response.Headers.Vary.Contains("Origin"),"Caché HTTP no separa los orígenes");
    await Get($"/cities?departamentoId={department.ToUpperInvariant()}");
    Check(hits==1,"UUID equivalente genera otra entrada");
    await Get($"/cities?departamentoId={Guid.NewGuid()}");Check(hits==2,"Mezcló departamentos");
    await Get(path,"Origin","https://a.example.invalid");await Get(path,"Origin","https://b.example.invalid");
    Check(hits==4,"Mezcló orígenes CORS");
    foreach (var header in new[]{"Authorization","Cookie"})
    {
        var before=hits;
        var a=await Get(path,header,header=="Cookie" ? "session=test" : "Bearer fake");await Get(path,header,"test");
        Check(hits==before+2 && a.Cache=="no-store","Cacheó credenciales o cookies");
    }
    foreach (var url in new[]{path+"&extra=1","/cities?departamentoId=invalid","/private","/error","/cookie","/large"})
    {
        var before=hits;var a=await Get(url);await Get(url);
        Check(hits==before+2,$"Cacheó entrada no admitida: {url.Split('?')[0]}");
        if (url!="/large") Check(a.Cache=="no-store","Error/privado no tiene no-store");
    }
    var beforeEvict=hits;
    await client.PostAsync("/evict",null);await Get(path);Check(hits==beforeEvict+1,"Invalidación no llega al origen");
    var beforeExpiry=hits;await Task.Delay(1200);await Get(path);Check(hits==beforeExpiry+1,"La entrada no expira");
    var oldQuery=client.GetStringAsync("/race");await raceEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
    raceValue=1;
    // Cambio concurrente sin pedir otro slot de lectura.
    await client.PostAsync("/evict",null);raceRelease.SetResult();await oldQuery;
    Check(await client.GetStringAsync("/race")=="1","Consulta anterior al cambio repobló caché obsoleta");
    var departmentFirst=await Get("/departments");var departmentAgain=await Get("/departments");
    Check(departmentFirst.Status==HttpStatusCode.OK && departmentFirst.Body==departmentAgain.Body && departmentHits==1,
        "Departamentos repetidos vuelven al origen");
    Check(departmentFirst.Cache.Contains("public"),"Departamentos anónimos no son cacheables");
    foreach (var header in new[]{"Authorization","Cookie"})
    {
        var before=departmentHits;
        var response=await Get("/departments",header,header=="Cookie" ? "session=test" : "Bearer fake");
        await Get("/departments",header,"test");
        Check(departmentHits==before+2 && response.Cache=="no-store","Departamentos cachean credenciales o cookies");
    }
    var beforeExtra=departmentHits;
    var extraDepartment=await Get("/departments?extra=1");await Get("/departments?extra=1");
    Check(departmentHits==beforeExtra+2 && extraDepartment.Cache=="no-store","Query extra de departamentos entra en caché");
    var beforeOrigin=departmentHits;
    await Get("/departments","Origin","https://a.example.invalid");
    await Get("/departments","Origin","https://b.example.invalid");
    Check(departmentHits==beforeOrigin+2,"Departamentos mezclan orígenes CORS");
    var beforeDepartmentEvict=departmentHits;
    await client.PostAsync("/evict",null);await Get("/departments");
    Check(departmentHits==beforeDepartmentEvict+1,"La invalidación geográfica no alcanza a departamentos");
    var beforeDepartmentExpiry=departmentHits;
    await Task.Delay(1200);await Get("/departments");
    Check(departmentHits==beforeDepartmentExpiry+1,"Departamentos no expiran");
    var inFlight=new List<Task<HttpResponseMessage>>();
    foreach (var kind in Enum.GetValues<ApiWorkload>())
    {
        inFlight.Add(client.GetAsync($"/hold/{kind}"));
        await held[kind].Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var excess=await client.GetAsync($"/hold/{kind}");
        Check(excess.StatusCode==HttpStatusCode.TooManyRequests && excess.Headers.RetryAfter is not null,$"No limita {kind}");
    }
    Check(inFlight.All(t=>!t.IsCompleted),"PDF/autenticación/escritura comparten el mismo slot");
    foreach (var gate in held.Values) gate.Release.TrySetResult();
    foreach (var response in await Task.WhenAll(inFlight)) {Check(response.StatusCode==HttpStatusCode.OK,"No liberó petición admitida");response.Dispose();}
    foreach (var expected in new[]{HttpStatusCode.OK,HttpStatusCode.OK,HttpStatusCode.TooManyRequests})
    {using var response=await client.PostAsync("/login-throttle",null);Check(response.StatusCode==expected,"Perdió límite por IP de login");}
}
finally {foreach (var gate in held.Values) gate.Release.TrySetResult();raceRelease.TrySetResult();await app.StopAsync();}
Console.WriteLine($"OK: {checks} comprobaciones de consumo, señales, worker, caché y límites; sin DB ni correos reales.");

sealed class RecordingEncuestaRepository : IEncuestaRepository
{
    public int DetailReads,ExistenceReads,Writes,ResponseReads;
    public Task<bool> ExisteAsync(Guid id,CancellationToken ct) {ExistenceReads++;return Task.FromResult(true);}
    public Task<bool> EditarAsync(Guid id,long revision,EncuestaEdicion datos,CancellationToken ct) {Writes++;return Task.FromResult(true);}
    public Task<bool> CambiarEstadoAsync(Guid id,long revision,bool publicar,DateOnly fecha,CancellationToken ct) {Writes++;return Task.FromResult(true);}
    public Task<IReadOnlyList<RespuestaEncuestaDatos>> RespuestasAsync(Guid id,Guid? cursor,int limite,CancellationToken ct) {ResponseReads++;return Task.FromResult<IReadOnlyList<RespuestaEncuestaDatos>>([]);}
    public Task<Domain.Entities.Encuesta?> ObtenerAsync(Guid id,bool administracion,DateOnly fecha,CancellationToken ct) {DetailReads++;throw new InvalidOperationException("No debe cargar la configuración en esta operación");}
    public Task<Guid> CrearAsync(EncuestaEdicion datos,Guid autor,CancellationToken ct)=>throw new NotSupportedException();
    public Task<IReadOnlyList<EncuestaResumen>> ListarAsync(Guid usuario,bool administracion,DateOnly fecha,Guid? cursor,int limite,CancellationToken ct)=>throw new NotSupportedException();
}

sealed class NoNetworkSender : ICorreoSender
{public Task<ResultadoCorreo> EnviarAsync(CorreoPendiente correo,CancellationToken ct)=>Task.FromResult(new ResultadoCorreo("enviado","fake"));}
sealed class RecordingAfiliacionRepository : ISolicitudesAfiliacionRepository
{
    public int Writes,ExistenceReads; public bool Result=true,Exists=true;
    public Task<bool> ExisteAsync(Guid id,CancellationToken ct) {ExistenceReads++;return Task.FromResult(Exists);}
    public Task<bool> AprobarAsync(Guid id,Guid admin,string? observacion,CancellationToken ct) {Writes++;return Task.FromResult(Result);}
    public Task<bool> RechazarAsync(Guid id,Guid admin,string motivo,CancellationToken ct) {Writes++;return Task.FromResult(Result);}
    public Task<IReadOnlyList<AfiliacionPendiente>> PendientesAsync(string estado,Guid? cursor,int limite,CancellationToken ct)=>throw new NotSupportedException();
    public Task<AfiliacionContacto?> ObtenerAsync(Guid id,CancellationToken ct)=>throw new Exception("No cargar contactos para resolver una solicitud");
    public Task<AfiliacionPropia?> ObtenerPropiaAsync(Guid usuario,CancellationToken ct)=>throw new NotSupportedException();
    public Task<AfiliacionTotales> TotalesAsync(CancellationToken ct)=>throw new NotSupportedException();
}
sealed class RecordingWakeSignal : ICorreoWakeSignal
{
    public int Count;
    public void Notify()=>Count++;
    public Task<bool> WaitAsync(TimeSpan timeout,CancellationToken ct)=>throw new NotSupportedException();
}
sealed class WorkerRepository(string scenario,CancellationTokenSource cancel) : INotificacionRepository
{
    public int Calls;public bool Finalized;
    public Task<NotificacionEstado?> EncolarAsync(string type,Guid id,CancellationToken ct)=>throw new NotSupportedException();
    public Task<CorreoPendiente?> ReservarAsync(int limit,CancellationToken ct)
    {
        Calls++;
        if (scenario=="error") {if (Calls==7) {cancel.Cancel();ct.ThrowIfCancellationRequested();} throw new IOException("Simulated");}
        return Task.FromResult<CorreoPendiente?>(scenario=="processed" && Calls==1 ? new(Guid.NewGuid(),"bienvenida","test@example.invalid",1) : null);
    }
    public Task FinalizarAsync(Guid id,string state,string? provider,string? error,int seconds,CancellationToken ct) {Finalized=true;return Task.CompletedTask;}
}
sealed class ScriptedSignal(string scenario,CancellationTokenSource cancel) : ICorreoWakeSignal
{
    public List<double> Waits=[];
    public void Notify() { }
    public Task<bool> WaitAsync(TimeSpan timeout,CancellationToken ct)
    {
        Waits.Add(timeout.TotalSeconds);
        if (Waits.Count==6 || scenario=="processed") cancel.Cancel();
        return Task.FromResult(scenario=="wake" && Waits.Count==2);
    }
}
// Solo acelera temporizadores de las pruebas, conservando los plazos solicitados para comprobarlos.
sealed class FastClock : TimeProvider
{
    public List<double> Delays=[];
    public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
    {Delays.Add(dueTime.TotalSeconds);return new FastTimer(callback,state);}
    private sealed class FastTimer(TimerCallback callback,object? state) : ITimer
    {
        private readonly Timer timer=new(callback,state,1,Timeout.Infinite);
        public bool Change(TimeSpan dueTime,TimeSpan period)=>timer.Change(dueTime,period);
        public void Dispose()=>timer.Dispose();
        public ValueTask DisposeAsync()=>timer.DisposeAsync();
    }
}
