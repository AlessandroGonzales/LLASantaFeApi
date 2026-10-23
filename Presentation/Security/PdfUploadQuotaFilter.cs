using Application.Interfaces;
using Domain.Models;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Presentation.Security;

// Resource filter: autorización y cuotas antes de que MVC lea/bufferice el multipart.
public sealed class PdfUploadQuotaFilter(ISolicitudesCiudadanaAppService solicitudes,ILogger<PdfUploadQuotaFilter> logger) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http=context.HttpContext;
        if (!http.Request.HasFormContentType) throw new BusinessException("validation", "Usá multipart/form-data con un único campo archivo.");
        var id=Guid.Parse((string)context.RouteData.Values["id"]!);
        var usuario=Guid.Parse(http.User.FindFirst("sub")!.Value);
        var values=http.Request.Headers[IdempotencyKey.Header];
        var clave=IdempotencyKey.Parse(values.ToString(),values.Count);
        var reserva=await solicitudes.ReservarPdfAsync(id,usuario,clave,http.RequestAborted);
        http.Items["PdfIntento"]=reserva;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var original=http.RequestAborted;
        http.RequestAborted=timeout.Token;
        var completed=false;
        try
        {
            var executed=await next();
            completed=executed.Exception is null && http.Response.StatusCode is >=200 and <300;
        }
        finally
        {
            http.RequestAborted=original;
            // Libera la reserva fallida, conservando el intento en la cuota de 24 h.
            // Si se pierde el proceso/DB, la reserva caduca por la ventana SQL de cinco minutos.
            if (clave is not null && !reserva.Confirmada && !completed)
            {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await solicitudes.FinalizarIntentoPdfAsync(reserva.Intento,usuario,cleanup.Token); }
                catch (Exception ex) { logger.LogWarning("No se pudo finalizar una reserva PDF. Tipo={ExceptionType}",ex.GetType().Name); }
            }
        }
    }
}
