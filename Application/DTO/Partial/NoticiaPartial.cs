using System.ComponentModel.DataAnnotations;
using Application.Validation;
using Domain.Models;
namespace Application.DTO.Partial;
public sealed class NoticiaPartial : IValidatableObject
{
    private readonly HashSet<string> campos=[];
    private string? titulo,resumen,contenido,imagenPrincipalUrl,categoria;
    private DateTimeOffset? fechaPublicacion;
    private bool? publicado,destacada;
    private Guid? ciudadId;
    [StringLength(200)] public string? Titulo {get=>titulo;set{titulo=value;campos.Add(nameof(Titulo));}}
    [StringLength(1000)] public string? Resumen {get=>resumen;set{resumen=value;campos.Add(nameof(Resumen));}}
    [StringLength(30000)] public string? Contenido {get=>contenido;set{contenido=value;campos.Add(nameof(Contenido));}}
    [StringLength(2048)] public string? ImagenPrincipalUrl {get=>imagenPrincipalUrl;set{imagenPrincipalUrl=value;campos.Add(nameof(ImagenPrincipalUrl));}}
    public DateTimeOffset? FechaPublicacion {get=>fechaPublicacion;set{fechaPublicacion=value;campos.Add(nameof(FechaPublicacion));}}
    public bool? Publicado {get=>publicado;set{publicado=value;campos.Add(nameof(Publicado));}}
    public bool? Destacada {get=>destacada;set{destacada=value;campos.Add(nameof(Destacada));}}
    [StringLength(80)] public string? Categoria {get=>categoria;set{categoria=value;campos.Add(nameof(Categoria));}}
    public Guid? CiudadId {get=>ciudadId;set{ciudadId=value;campos.Add(nameof(CiudadId));}}
    public NoticiaCambios ToChanges()=>new(
        new(campos.Contains(nameof(Titulo)),Titulo?.Trim()),new(campos.Contains(nameof(Resumen)),Resumen?.Trim()),
        new(campos.Contains(nameof(Contenido)),Contenido?.Trim()),new(campos.Contains(nameof(ImagenPrincipalUrl)),ImagenPrincipalUrl),
        new(campos.Contains(nameof(FechaPublicacion)),FechaPublicacion?.UtcDateTime),new(campos.Contains(nameof(Publicado)),Publicado??false),
        new(campos.Contains(nameof(Destacada)),Destacada??false),new(campos.Contains(nameof(Categoria)),Categoria?.Trim()),
        new(campos.Contains(nameof(CiudadId)),CiudadId));
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (campos.Count==0) yield return new("Incluí al menos un atributo editable.");
        if (campos.Contains(nameof(Titulo)) && (Titulo?.Trim().Length??0)<3) yield return new("El título requiere al menos 3 caracteres.",[nameof(Titulo)]);
        if (campos.Contains(nameof(Contenido)) && (Contenido?.Trim().Length??0)<10) yield return new("El contenido requiere al menos 10 caracteres.",[nameof(Contenido)]);
        if (campos.Contains(nameof(Publicado)) && Publicado is null) yield return new("Publicado no puede ser null.",[nameof(Publicado)]);
        if (campos.Contains(nameof(Destacada)) && Destacada is null) yield return new("Destacada no puede ser null.",[nameof(Destacada)]);
        if (!UsuarioRules.IsHttpsUrl(ImagenPrincipalUrl)) yield return new("La imagen debe ser una URL HTTPS sin credenciales.",[nameof(ImagenPrincipalUrl)]);
        if (CiudadId==Guid.Empty) yield return new("La ciudad no es válida.",[nameof(CiudadId)]);
    }
}
