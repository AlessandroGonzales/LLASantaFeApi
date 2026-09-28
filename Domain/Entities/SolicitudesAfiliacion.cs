namespace Domain.Entities
{
    public class SolicitudesAfiliacion
    {
        public Guid Id { get; set; }
        public DateTime? AprobadaAt { get; set; }
        public Guid? AprobadaPor { get; set; }

        public Guid UsuarioId { get; set; }

        public DateTime FechaSolicitud { get; set; }

        public string Estado { get; set; } = null!;

        public string? Observacion { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual Usuario Usuario { get; set; } = null!;
    }
}
