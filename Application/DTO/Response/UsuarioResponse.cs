
namespace Application.DTO.Response
{
    public class UsuarioResponse
    {
        public Guid IdUsuario { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Email { get; set; } 
        public string? Telefono { get; set; }
        public string? Genero { get; set; }
        public string? Profesion { get; set; }
        public string? FotoPerfilUrl { get; set; }
        public Guid? CiudadId { get; set; }
        public string? RoleNombre { get; set; }
    }
}
