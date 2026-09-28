using System.ComponentModel.DataAnnotations;
namespace Application.DTO.Request;
public sealed class SolicitudGestionRequest
{
    [Range(1,long.MaxValue)] public long Revision { get; set; }
    [Required, RegularExpression("^(en_revision|resuelta|rechazada)$")] public string Estado { get; set; } = "";
    [StringLength(5000)] public string? Respuesta { get; set; }
}
