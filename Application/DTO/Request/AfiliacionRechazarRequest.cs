using System.ComponentModel.DataAnnotations;

namespace Application.DTO.Request;

public sealed class AfiliacionRechazarRequest
{
    // Visible para el solicitante; no es una observación administrativa interna.
    [Required, StringLength(2000, MinimumLength = 10)]
    public string Motivo { get; set; } = string.Empty;
}
