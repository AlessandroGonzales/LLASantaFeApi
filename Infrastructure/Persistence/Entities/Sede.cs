using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class Sede
{
    public Guid Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string Direccion { get; set; } = null!;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Horario { get; set; }

    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? ImagenUrl { get; set; }

    public Guid? CiudadId { get; set; }

    public virtual Ciudade? Ciudad { get; set; }
}
