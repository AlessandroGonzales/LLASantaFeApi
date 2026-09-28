namespace Domain.Entities
{
    public class SolicitudesCiudadana
    {
        public Guid Id { get; set; }
        public string Estado { get; set; } = "pendiente";
        public string? Respuesta { get; set; }
        public long Revision { get; set; }
        public Guid? GestionadoPor { get; set; }
        public Guid UsuarioId { get; set; }
        public string Motivo { get; set; } = null!;
        public string Mensaje { get; set; } = null!;
        public string? PdfUrl { get; set; }
        public bool Aceptacion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public virtual Usuario Usuario { get; set; } = null!;
    }
}
