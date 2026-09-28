namespace Domain.Entities
{
    public class Sede
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Direccion { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Horario { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public string? ImagenUrl { get; set; }
        public Guid? CiudadId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public virtual Departamento? Departamento { get; set; }
    }
}
