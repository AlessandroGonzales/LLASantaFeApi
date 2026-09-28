using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Presentation.Security;
// Singleton: cuota por identidad validada, independiente de la IP.
public sealed class PdfAccountRateFilter : IAsyncResourceFilter, IDisposable
{
    private readonly PartitionedRateLimiter<HttpContext> limiter = PartitionedRateLimiter.Create<HttpContext,string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(http.User.FindFirst("sub")!.Value, _ => new FixedWindowRateLimiterOptions
        { PermitLimit=20, Window=TimeSpan.FromMinutes(1), QueueLimit=0, AutoReplenishment=true }));
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        using var lease=await limiter.AcquireAsync(context.HttpContext,1,context.HttpContext.RequestAborted);
        if(!lease.IsAcquired)
        {
            context.HttpContext.Response.Headers.RetryAfter="60";
            context.Result=new ObjectResult(new {status=429,title="Demasiadas transferencias PDF para esta cuenta."}) { StatusCode=429 };
            return;
        }
        await next();
    }
    public void Dispose() => limiter.Dispose();
}
