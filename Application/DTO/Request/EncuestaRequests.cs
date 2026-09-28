using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace Application.DTO.Request;

public class EncuestaCrearRequest
{
    [Required, StringLength(200, MinimumLength = 3)] public string Titulo { get; set; } = "";
    [StringLength(2000)] public string? Descripcion { get; set; }
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    [Required, StringLength(50)] public string Tipo { get; set; } = "general";
    public JsonElement Configuracion { get; set; }
    public Guid? CiudadId { get; set; }
    public Guid? DepartamentoId { get; set; }
}
public sealed class EncuestaEditarRequest : EncuestaCrearRequest
{
    [Range(1, long.MaxValue)] public long Revision { get; set; }
}
public sealed class EncuestaEstadoRequest
{
    [Range(1, long.MaxValue)] public long Revision { get; set; }
}
