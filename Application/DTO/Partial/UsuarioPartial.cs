namespace Application.DTO.Partial
{
    public class UsuarioPartial
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? FotoPerfilUrl { get; set; }
        public Guid? CiudadId { get; set; }
    }
}
