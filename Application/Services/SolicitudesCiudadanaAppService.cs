using System.Security.Cryptography;
using System.Text;
using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class SolicitudesCiudadanaAppService(ISolicitudesCiudadanaRepository solicitudes, IPdfScanner scanner) : ISolicitudesCiudadanaAppService
{
    public const int MaxPdfBytes = 2 * 1024 * 1024;
    public async Task<PaginaSolicitudes> ListarAsync(Guid usuario, bool admin, string? estado, Guid? cursor, int limite, CancellationToken ct)
    {
        if (limite is < 1 or > 50 || estado is not (null or "pendiente" or "en_revision" or "resuelta" or "rechazada"))
            throw new BusinessException("validation", "Límite (1–50) o estado inválido.");
        var rows = await solicitudes.ListarAsync(usuario, admin, estado, cursor, limite + 1, ct);
        return new(rows.Take(limite).ToArray(), rows.Count > limite ? rows[limite - 1].Id : null);
    }
    public async Task<SolicitudDetalle> ObtenerAsync(Guid id, Guid usuario, bool admin, CancellationToken ct) =>
        await solicitudes.ObtenerAsync(id, usuario, admin, ct) ?? throw new BusinessException("not_found", "Solicitud no disponible.");
    public async Task GestionarAsync(Guid id, Guid admin, SolicitudGestionRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (request.Estado is "resuelta" or "rechazada" && (request.Respuesta?.Trim().Length ?? 0) < 10)
            throw new BusinessException("validation", "Indicá una respuesta de al menos 10 caracteres para finalizar la solicitud.");
        await ObtenerAsync(id, admin, true, ct);
        if (!await solicitudes.GestionarAsync(id, admin, request.Revision, request.Estado, request.Respuesta?.Trim(), ct))
            throw new BusinessException("conflict", "La solicitud cambió o ya está finalizada. Volvé a consultarla.");
    }
    public Task<Guid> ReservarPdfAsync(Guid id, Guid usuario, CancellationToken ct) => solicitudes.ReservarPdfAsync(id, usuario, ct);
    public async Task AdjuntarAsync(Guid id, Guid usuario, Guid intento, Stream contenido, long length, string nombre, string contentType, CancellationToken ct)
    {
        if (length > MaxPdfBytes) throw new BusinessException("too_large", "El PDF no puede superar 2 MiB.");
        if (length < 20 || nombre.Length > 180 || !nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("validation", "Adjuntá un archivo PDF válido.");
        var bytes = new byte[(int)length];
        try { await contenido.ReadExactlyAsync(bytes, ct); }
        catch (EndOfStreamException) { throw new BusinessException("validation", "El archivo está incompleto."); }
        var extra = new byte[1];
        if (await contenido.ReadAsync(extra, ct) != 0) throw new BusinessException("too_large", "Longitud de archivo inválida.");
        // Es un filtro de formato, no sustituye al antivirus ni prueba que un PDF sea inocuo.
        if (!bytes.AsSpan().StartsWith("%PDF-"u8) || !Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal))
            throw new BusinessException("validation", "El archivo no tiene encabezado y cierre PDF válidos.");
        if (!await scanner.EsLimpioAsync(bytes, ct))
            throw new BusinessException("validation", "El análisis de seguridad rechazó el archivo.");
        await solicitudes.GuardarPdfAsync(id, usuario, intento, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), ct);
    }
    public async Task<byte[]> DescargarAsync(Guid id, Guid usuario, bool admin, CancellationToken ct) =>
        (await solicitudes.ObtenerPdfAsync(id, usuario, admin, ct) ?? throw new BusinessException("not_found", "Adjunto no disponible.")).Contenido;
}
