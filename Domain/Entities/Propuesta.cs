namespace Domain.Entities
{
    public class Propuesta
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public string? ImagenUrl { get; set; }
        public string? DocumentoPdfUrl { get; set; }
        public string? Tematica { get; set; }
        public Guid? DepartamentoId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public virtual Departamento? Departamento { get; set; }
    }
}
