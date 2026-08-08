namespace Domain.Entities
{
    public class Ciudade
    {
        public Guid Id { get; set; }

        public Guid DepartamentoId { get; set; }

        public string Nombre { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual Departamento Departamento { get; set; } = null!;

        public virtual ICollection<Encuesta> Encuesta { get; set; } = new List<Encuesta>();

        public virtual ICollection<Noticia> Noticia { get; set; } = new List<Noticia>();

        public virtual ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
