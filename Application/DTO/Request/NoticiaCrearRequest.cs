using System.ComponentModel.DataAnnotations;
using Application.Validation;
namespace Application.DTO.Request;
public sealed class NoticiaCrearRequest : IValidatableObject
{
    [Required,StringLength(200,MinimumLength=3)] public string Titulo { get; set; } = "";
    [StringLength(1000)] public string? Resumen { get; set; }
    [Required,StringLength(30000,MinimumLength=10)] public string Contenido { get; set; } = "";
    [StringLength(2048)] public string? ImagenPrincipalUrl { get; set; }
    public DateTimeOffset? FechaPublicacion { get; set; }
    public bool Publicado { get; set; }
    public bool Destacada { get; set; }
    [StringLength(80)] public string? Categoria { get; set; }
    public Guid? CiudadId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if ((Titulo?.Trim().Length ?? 0)<3) yield return new("El título requiere al menos 3 caracteres.",[nameof(Titulo)]);
        if ((Contenido?.Trim().Length ?? 0)<10) yield return new("El contenido requiere al menos 10 caracteres.",[nameof(Contenido)]);
        if (!UsuarioRules.IsHttpsUrl(ImagenPrincipalUrl)) yield return new("La imagen debe ser una URL HTTPS sin credenciales.",[nameof(ImagenPrincipalUrl)]);
        if (CiudadId==Guid.Empty) yield return new("La ciudad no es válida.",[nameof(CiudadId)]);
    }
}
