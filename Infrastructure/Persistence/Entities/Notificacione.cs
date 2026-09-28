using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class Notificacione
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public string Tipo { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? Url { get; set; }
}
