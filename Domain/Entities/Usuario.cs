
namespace Domain.Entities;

public class Usuario
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Apellido { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Telefono { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string? Profesion { get; set; }
    public string? Genero { get; set; }
    public string? FotoPerfilUrl { get; set; }
    public DateTime FechaRegistro { get; set; }
    public bool Activo { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CiudadId { get; set; }
    public virtual Ciudade? Ciudad { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? RolId { get; set; }
    public string? RoleNombre { get; set; } 
    public virtual Role? Rol { get; set; }
    public virtual Usuario? CreatedByNavigation { get; set; }
    public virtual ICollection<Encuesta> Encuesta { get; set; } = new List<Encuesta>();
    public virtual ICollection<EncuestaRespuesta> EncuestaRespuesta { get; set; } = new List<EncuestaRespuesta>();
    public virtual ICollection<Usuario> InverseCreatedByNavigation { get; set; } = new List<Usuario>();
    public virtual ICollection<Noticia> Noticia { get; set; } = new List<Noticia>();
    public virtual ICollection<Notificacione> Notificaciones { get; set; } = new List<Notificacione>();
    public virtual ICollection<SolicitudesAfiliacion> SolicitudesAfiliacions { get; set; } = new List<SolicitudesAfiliacion>();
    public virtual ICollection<SolicitudesCiudadana> SolicitudesCiudadanas { get; set; } = new List<SolicitudesCiudadana>();
}
