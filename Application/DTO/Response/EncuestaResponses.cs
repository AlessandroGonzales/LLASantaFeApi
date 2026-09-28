using System.Text.Json;
namespace Application.DTO.Response;
public sealed record PaginaEncuesta<T>(IReadOnlyList<T> Items, Guid? SiguienteCursor);
public sealed record EncuestaDetalleResponse(Guid Id, string Titulo, string? Descripcion, string Tipo,
    DateOnly? FechaInicio, DateOnly? FechaFin, bool Activa, DateTime? PublicadaAt, long Revision,
    Guid? CiudadId, Guid? DepartamentoId, JsonElement Configuracion);
public sealed record RespuestaEncuestaResponse(Guid Id, JsonElement Respuestas, DateTime CreatedAt);
