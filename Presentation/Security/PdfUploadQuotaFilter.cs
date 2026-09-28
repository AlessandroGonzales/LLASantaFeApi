using Application.Interfaces;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Presentation.Security;

// Resource filter: autorización y cuotas antes de que MVC lea/bufferice el multipart.
public sealed class PdfUploadQuotaFilter(ISolicitudesCiudadanaAppService solicitudes) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http=context.HttpContext;
        if (!http.Request.HasFormContentType) throw new BusinessException("validation", "Usá multipart/form-data con un único campo archivo.");
        var id=Guid.Parse((string)context.RouteData.Values["id"]!);
        var usuario=Guid.Parse(http.User.FindFirst("sub")!.Value);
        http.Items["PdfIntento"]=await solicitudes.ReservarPdfAsync(id,usuario,http.RequestAborted);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var original=http.RequestAborted;
        http.RequestAborted=timeout.Token;
        try { await next(); }
        finally { http.RequestAborted=original; }
    }
}
