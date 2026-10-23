namespace Domain.Models;

public sealed record EncuestaEdicion(string Titulo, string? Descripcion, DateOnly? FechaInicio,
    DateOnly? FechaFin, string Tipo, string Configuracion, Guid? CiudadId, Guid? DepartamentoId);
public sealed record EncuestaResumen(Guid Id, string Titulo, string? Descripcion, string Tipo,
    DateOnly? FechaInicio, DateOnly? FechaFin, bool Activa, DateTime? PublicadaAt,
    long Revision, bool Respondida);
public sealed record RespuestaEncuestaDatos(Guid Id, string Respuestas, DateTime CreatedAt);
// Comprobante propio: no necesita cargar el JSON de respuestas ni el de preguntas.
public sealed record EncuestaRespuestaComprobante(Guid Id, DateTime CreatedAt);
