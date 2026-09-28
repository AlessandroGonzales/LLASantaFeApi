using System.ComponentModel.DataAnnotations;
namespace Application.DTO.Request;
public sealed class NotificacionRequest : IValidatableObject
{
    [Required,RegularExpression("^(bienvenida|afiliacion_aprobada)$")]
    public string Tipo { get; set; } = "";
    public Guid ReferenciaId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ReferenciaId==Guid.Empty) yield return new("La referencia es obligatoria.",[nameof(ReferenciaId)]);
    }
}
