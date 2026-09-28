using System.ComponentModel.DataAnnotations;
namespace Application.DTO.Request;
public sealed class AfiliacionAprobarRequest
{
    [StringLength(2000)] public string? Observacion { get; set; }
}
