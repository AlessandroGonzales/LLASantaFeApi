namespace Application.DTO.Response;
public sealed class UsuarioResponse
{
    public Guid IdUsuario { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Apellido { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailVerificado { get; init; }
    public bool RequiereVerificarEmail { get; init; }
    public string? Telefono { get; init; }
    public DateOnly? FechaNacimiento { get; init; }
    public string? Genero { get; init; }
    public string? Profesion { get; init; }
    public string? FotoPerfilUrl { get; init; }
    public Guid? CiudadId { get; init; }
    public string? RoleNombre { get; init; }
    public DateTime FechaRegistro { get; init; }
}
