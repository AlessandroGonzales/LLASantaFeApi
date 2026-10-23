using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Security;
public static class ApiRateLimits
{
    public static IServiceCollection AddApiRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var limits=configuration.GetSection("RateLimits").Get<ApiConsumptionOptions>() ?? new();
        limits.Validate();
        var connectionBudget=configuration.GetValue<int?>("DatabaseConsumption:ApiMaxPoolSize") ?? 20;
        if (limits.ReadConcurrency+limits.WriteConcurrency+limits.AuthenticationConcurrency+limits.PdfConcurrency>connectionBudget)
            throw new InvalidOperationException("La concurrencia total de RateLimits no puede superar DatabaseConsumption:ApiMaxPoolSize.");
        services.AddSingleton(limits);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(Ip(context), _ => Window(limits.General, TimeSpan.FromMinutes(1)))),
                PartitionedRateLimiter.Create<HttpContext, ApiWorkload>(context =>
                {
                    var workload=Workload(context);
                    var permits=workload switch
                    {
                        ApiWorkload.Authentication => limits.AuthenticationConcurrency,
                        ApiWorkload.Pdf => limits.PdfConcurrency,
                        ApiWorkload.Write => limits.WriteConcurrency,
                        _ => limits.ReadConcurrency
                    };
                    return RateLimitPartition.GetConcurrencyLimiter(workload, _ => new ConcurrencyLimiterOptions
                    { PermitLimit=permits, QueueLimit=0 });
                }));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(limits.Login, TimeSpan.FromMinutes(1))));
            options.AddPolicy("registro", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(limits.Registro, TimeSpan.FromHours(1))));
            options.AddPolicy("escritura", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(limits.Escritura, TimeSpan.FromMinutes(1))));
            options.AddPolicy("cuenta_correo", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(limits.CuentaCorreo, TimeSpan.FromHours(1))));
            options.OnRejected = async (rejected, ct) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    rejected.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                else rejected.HttpContext.Response.Headers.RetryAfter="2";
                rejected.HttpContext.Response.Headers.CacheControl = "no-store";
                await rejected.HttpContext.Response.WriteAsJsonAsync(new
                { status = 429, title = "Demasiadas solicitudes. Intentá nuevamente más tarde." }, ct);
            };
        });
        return services;
    }
    private static ApiWorkload Workload(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<ApiWorkloadAttribute>()?.Workload ??
        (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method)
            ? ApiWorkload.Read : ApiWorkload.Write);
    // No leer X-Forwarded-For del cliente. Configurar proxies de confianza al desplegar.
    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private static FixedWindowRateLimiterOptions Window(int limit, TimeSpan window) => new()
    { PermitLimit = limit, Window = window, QueueLimit = 0, AutoReplenishment = true };
}
