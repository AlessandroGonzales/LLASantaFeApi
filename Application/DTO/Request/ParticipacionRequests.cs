using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace Application.DTO.Request;
public sealed class EncuestaRespuestaRequest
{
    [Required] public Dictionary<string, JsonElement> Respuestas { get; set; } = [];
}
public sealed class SolicitudCiudadanaRequest
{
    [Required, RegularExpression("^(proyecto|sugerencia|duda|voluntariado|denuncia)$",
        ErrorMessage = "Motivo inválido. Usá proyecto, sugerencia, duda, voluntariado o denuncia (el value del frontend).")]
    public string Motivo { get; set; } = string.Empty;
    [Required, StringLength(5000, MinimumLength = 10)] public string Mensaje { get; set; } = string.Empty;
}
public sealed class AfiliacionRequest : IValidatableObject
{
    public bool Confirmacion { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Confirmacion) yield return new("Debés confirmar la solicitud de afiliación.", [nameof(Confirmacion)]);
    }
}
