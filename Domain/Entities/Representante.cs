namespace Domain.Entities
{
    public class Representante
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Cargo { get; set; } = null!;
        public string? Profesion { get; set; }
        public string? Descripcion { get; set; }
        public string? Biografia { get; set; }
        public string? FotoUrl { get; set; }
        public string? Instagram { get; set; }
        public string? Twitter { get; set; }
        public string? Facebook { get; set; }
        public string? Email { get; set; }
        public bool Activo { get; set; }
        public string? Proyectos { get; set; }
        public Guid? DepartamentoId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public virtual Departamento? Departamento { get; set; }
    }
}
