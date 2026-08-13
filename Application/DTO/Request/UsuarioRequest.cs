namespace Application.DTO.Request
{
    public class UsuarioRequest
    {
        public string Nombre { get; set; } = null!;
        public string Apellido { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Telefono { get; set; }
        public string? Genero { get; set; }
        public string? Profesion { get; set; }
        public string? FotoPerfilUrl { get; set; }
        public DateOnly FechaNacimiento { get; set; }
        public Guid? CiudadId { get; set; }

    }
}
