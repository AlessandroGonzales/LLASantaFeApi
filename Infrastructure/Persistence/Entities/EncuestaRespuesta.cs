using System.Net;

namespace Infrastructure.Persistence.Entities;

public partial class EncuestaRespuesta
{
    public Guid Id { get; set; }

    public Guid EncuestaId { get; set; }

    public Guid? UsuarioId { get; set; }

    public string Respuestas { get; set; } = null!;

    public string? Metadata { get; set; }

    public IPAddress? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Encuesta Encuesta { get; set; } = null!;

    public virtual Usuario? Usuario { get; set; }
}
