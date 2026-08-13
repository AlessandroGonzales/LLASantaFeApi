using System;
using System.Collections.Generic;

namespace Infrastructure.Persistence.Entities;

public partial class Evento
{
    public Guid Id { get; set; }

    public string Titulo { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateOnly Fecha { get; set; }

    public TimeOnly? Hora { get; set; }

    public string? Direccion { get; set; }

    public string? ImagenUrl { get; set; }

    public int? CantidadMaxima { get; set; }

    public Guid? DepartamentoId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Departamento? Departamento { get; set; }
}
