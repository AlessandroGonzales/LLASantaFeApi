namespace Infrastructure.Persistence.Entities;

public partial class Notificacione
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public bool Leida { get; set; }

    public string Tipo { get; set; } = null!;

    public Guid? UsuarioId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Usuario? Usuario { get; set; }
}
