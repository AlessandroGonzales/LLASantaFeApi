namespace Infrastructure.Persistence.Entities;
public partial class Noticia
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = null!;

    public string? Resumen { get; set; }

    public string Contenido { get; set; } = null!;

    public string? ImagenPrincipalUrl { get; set; }

    public DateTime? FechaPublicacion { get; set; }

    public bool Publicado { get; set; }

    public bool Destacada { get; set; }

    public int CantidadVisualizaciones { get; set; }

    public string? Categoria { get; set; }

    public Guid? CiudadId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public virtual Ciudade? Ciudad { get; set; }

    public virtual Usuario? CreatedByNavigation { get; set; }
}
