using System.ComponentModel.DataAnnotations;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Presentation.Security;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            BusinessException { Code: "unauthorized" } ex => (401, ex.Message),
            BusinessException { Code: "email_not_verified" } ex => (403, ex.Message),
            BusinessException { Code: "not_found" } ex => (404, ex.Message),
            BusinessException { Code: "conflict" } ex => (409, ex.Message),
            BusinessException { Code: "validation" } ex => (400, ex.Message),
            BusinessException { Code: "too_large" } ex => (413, ex.Message),
            BusinessException { Code: "rate_limit" } ex => (429, ex.Message),
            BusinessException { Code: "unavailable" } ex => (503, ex.Message),
            ValidationException => (400, "Los datos enviados no son válidos."),
            BadHttpRequestException ex => (ex.StatusCode, "La solicitud no es válida."),
            _ => DatabaseError(exception)
        };
        if (status >= 500)
            logger.LogError("Fallo de API. TraceId={TraceId}, Tipo={ExceptionType}", context.TraceIdentifier, exception.GetType().Name);
        context.Response.StatusCode = status;
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        if (postgres?.SqlState == "P0425") context.Response.Headers.RetryAfter="30";
        context.Response.Headers.CacheControl = "no-store";
        if (status == 401) context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status, Title = detail,
            Extensions = { ["traceId"] = context.TraceIdentifier,
                ["code"] = exception is BusinessException business ? business.Code : postgres?.SqlState switch
                { "P0412" => "idempotency_conflict", "P0425" => "upload_in_progress", _ => "request_failed" } }
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json; charset=utf-8", cancellationToken: ct);
        return true;
    }
    private static (int, string) DatabaseError(Exception exception)
    {
        var pg = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        if (pg?.SqlState == "P0401") return (401, "Acceso no vigente.");
        if (pg?.SqlState == "P0412") return (409, "La clave de idempotencia corresponde a otros datos.");
        if (pg?.SqlState == "P0425") return (409, "Hay una carga en curso para esta clave. Consultá el estado y reintentá más tarde.");
        if (pg?.SqlState == "P0429") return (429, "Alcanzaste la cuota de solicitudes o adjuntos. Intentá más tarde.");
        if (pg?.SqlState == "P0404") return (404, "Solicitud no disponible.");
        if (pg?.SqlState == "P0409") return (409, "La solicitud ya no admite esta acción.");
        if (pg?.SqlState == "P0503") return (503, "Se alcanzó la capacidad de adjuntos. La solicitud de texto sigue disponible.");
        if (pg is { SqlState: PostgresErrorCodes.UniqueViolation })
            return (409, pg.ConstraintName switch
            {
                "ux_encuesta_respuestas_usuario" => "Ya respondiste esta encuesta.",
                "solicitudes_afiliacion_usuario_key" => "Ya existe tu solicitud de afiliación.",
                _ => "El registro ya existe."
            });
        if (pg?.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation)
            return (400, "Los datos no cumplen las reglas del registro.");
        return exception is NpgsqlException or DbUpdateException { InnerException: NpgsqlException }
            ? (503, "Servicio temporalmente no disponible.") : (500, "No se pudo completar la operación.");
    }
}
