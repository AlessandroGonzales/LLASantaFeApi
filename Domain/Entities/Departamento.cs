namespace Domain.Entities
{
    public class Departamento
    {
        public Guid Id { get; set; }

        public string Nombre { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual ICollection<Ciudade> Ciudades { get; set; } = new List<Ciudade>();

        public virtual ICollection<Encuesta> Encuesta { get; set; } = new List<Encuesta>();

        public virtual ICollection<Evento> Eventos { get; set; } = new List<Evento>();

        public virtual ICollection<Propuesta> Propuesta { get; set; } = new List<Propuesta>();

        public virtual ICollection<Representante> Representantes { get; set; } = new List<Representante>();

        public virtual ICollection<Sede> Sedes { get; set; } = new List<Sede>();
    }
}
