using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace Presentation.Security;
public static class ApiRateLimits
{
    public static IServiceCollection AddApiRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(Ip(context), _ => Window(Limit("General", 120), TimeSpan.FromMinutes(1)))),
                PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                    RateLimitPartition.GetConcurrencyLimiter("api", _ => new ConcurrencyLimiterOptions
                    { PermitLimit = 8, QueueLimit = 0 })));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(Limit("Login", 10), TimeSpan.FromMinutes(1))));
            options.AddPolicy("registro", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(Limit("Registro", 5), TimeSpan.FromHours(1))));
            options.AddPolicy("escritura", context => RateLimitPartition.GetFixedWindowLimiter(
                Ip(context), _ => Window(Limit("Escritura", 20), TimeSpan.FromMinutes(1))));
            options.OnRejected = async (rejected, ct) =>
            {
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    rejected.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                rejected.HttpContext.Response.Headers.CacheControl = "no-store";
                await rejected.HttpContext.Response.WriteAsJsonAsync(new
                { status = 429, title = "Demasiadas solicitudes. Intentá nuevamente más tarde." }, ct);
            };
        });
        return services;
        int Limit(string name, int fallback) => Math.Clamp(configuration.GetValue<int?>($"RateLimits:{name}") ?? fallback, 1, 10000);
    }
    // No leer X-Forwarded-For del cliente. Configurar proxies de confianza al desplegar.
    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    private static FixedWindowRateLimiterOptions Window(int limit, TimeSpan window) => new()
    { PermitLimit = limit, Window = window, QueueLimit = 0, AutoReplenishment = true };
}
