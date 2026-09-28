namespace Domain.Entities
{
    public class Encuesta
    {
        public Guid Id { get; set; }
        public DateTime? PublicadaAt { get; set; }
        public long Revision { get; set; }
        public string Titulo { get; set; } = null!;
        public string? Descripcion { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public bool Activa { get; set; }
        public string Tipo { get; set; } = null!;
        public string Configuracion { get; set; } = null!;
        public Guid? CiudadId { get; set; }
        public Guid? DepartamentoId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public virtual Ciudade? Ciudad { get; set; }
        public virtual Usuario? CreatedByNavigation { get; set; }
        public virtual Departamento? Departamento { get; set; }
        public virtual ICollection<EncuestaRespuesta> EncuestaRespuesta { get; set; } = new List<EncuestaRespuesta>();
    }
}
