using System.Text.Json;
using System.Text.RegularExpressions;
using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;

public sealed class EncuestaAppService(IEncuestaRepository encuestas, TimeProvider clock) : IEncuestaAppService
{
    private static readonly TimeZoneInfo SantaFe = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
    private DateOnly Hoy => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), SantaFe).DateTime);
    public async Task<PaginaEncuesta<EncuestaResumen>> ListarAsync(Guid usuario, bool admin, Guid? cursor, int limite, CancellationToken ct)
    {
        ValidarLimite(limite, 50);
        var rows = await encuestas.ListarAsync(usuario, admin, Hoy, cursor, limite + 1, ct);
        return new(rows.Take(limite).ToArray(), rows.Count > limite ? rows[limite - 1].Id : null);
    }
    public async Task<EncuestaDetalleResponse> ObtenerAsync(Guid id, bool admin, CancellationToken ct)
    {
        var e = await encuestas.ObtenerAsync(id, admin, Hoy, ct) ?? throw new BusinessException("not_found", "Encuesta no disponible.");
        return new(e.Id, e.Titulo, e.Descripcion, e.Tipo, e.FechaInicio, e.FechaFin, e.Activa,
            e.PublicadaAt, e.Revision, e.CiudadId, e.DepartamentoId, JsonSerializer.Deserialize<JsonElement>(e.Configuracion));
    }
    public Task<Guid> CrearAsync(EncuestaCrearRequest request, Guid autor, CancellationToken ct) =>
        encuestas.CrearAsync(Validar(request), autor, ct);
    public async Task EditarAsync(Guid id, EncuestaEditarRequest request, CancellationToken ct)
    {
        var datos = Validar(request);
        await ObtenerAsync(id, true, ct);
        if (request.Revision < 1 || !await encuestas.EditarAsync(id, request.Revision, datos, ct)) throw Conflicto();
    }
    public async Task CambiarEstadoAsync(Guid id, EncuestaEstadoRequest request, bool publicar, CancellationToken ct)
    {
        var e = await ObtenerAsync(id, true, ct);
        if (publicar) EncuestaValidator.ValidateConfiguration(e.Configuracion);
        if (request.Revision < 1 || !await encuestas.CambiarEstadoAsync(id, request.Revision, publicar, Hoy, ct)) throw Conflicto();
    }
    public async Task<PaginaEncuesta<RespuestaEncuestaResponse>> RespuestasAsync(Guid id, Guid? cursor, int limite, CancellationToken ct)
    {
        ValidarLimite(limite, 20);
        await ObtenerAsync(id, true, ct);
        var rows = await encuestas.RespuestasAsync(id, cursor, limite + 1, ct);
        return new(rows.Take(limite).Select(r => new RespuestaEncuestaResponse(r.Id,
            JsonSerializer.Deserialize<JsonElement>(r.Respuestas), r.CreatedAt)).ToArray(),
            rows.Count > limite ? rows[limite - 1].Id : null);
    }
    private static EncuestaEdicion Validar(EncuestaCrearRequest r)
    {
        UsuarioRules.Validate(r);
        if (string.IsNullOrWhiteSpace(r.Titulo) || r.Titulo.Trim().Length < 3 ||
            !Regex.IsMatch(r.Tipo, "^[a-z][a-z0-9_]{0,49}$", RegexOptions.CultureInvariant) ||
            r.FechaInicio > r.FechaFin || r.CiudadId == Guid.Empty || r.DepartamentoId == Guid.Empty ||
            (r.CiudadId is not null && r.DepartamentoId is not null))
            throw new BusinessException("validation", "Título, tipo, fechas o ubicación inválidos. Elegí ciudad o departamento, no ambos.");
        return new(r.Titulo.Trim(), r.Descripcion?.Trim(), r.FechaInicio, r.FechaFin, r.Tipo,
            EncuestaValidator.ValidateConfiguration(r.Configuracion), r.CiudadId, r.DepartamentoId);
    }
    private static void ValidarLimite(int limite, int max)
    {
        if (limite < 1 || limite > max) throw new BusinessException("validation", $"El límite debe estar entre 1 y {max}.");
    }
    private static BusinessException Conflicto() => new("conflict", "La encuesta cambió, ya fue publicada/cerrada o sus fechas no permiten esta acción. Volvé a consultarla.");
}
