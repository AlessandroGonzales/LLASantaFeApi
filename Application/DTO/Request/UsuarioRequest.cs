using System.ComponentModel.DataAnnotations;
using Application.Validation;
namespace Application.DTO.Request;
public sealed class UsuarioRequest : IValidatableObject
{
    [Required, StringLength(100)] public string Nombre { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Apellido { get; set; } = string.Empty;
    [Required, StringLength(254), RegularExpression(UsuarioRules.EmailPattern)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(72, MinimumLength = 15)] public string Password { get; set; } = string.Empty;
    [RegularExpression(UsuarioRules.PhonePattern)] public string? Telefono { get; set; }
    [RegularExpression("^(masculino|femenino|otro|prefiero_no_decir)$")] public string? Genero { get; set; }
    [StringLength(150)] public string? Profesion { get; set; }
    [StringLength(2048)] public string? FotoPerfilUrl { get; set; }
    public DateOnly FechaNacimiento { get; set; }
    public Guid? CiudadId { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!UsuarioRules.PasswordFitsBcrypt(Password))
            yield return new("La contraseña no puede superar 72 bytes UTF-8.", [nameof(Password)]);
        if (FechaNacimiento == default || FechaNacimiento > DateOnly.FromDateTime(DateTime.UtcNow))
            yield return new("La fecha de nacimiento es inválida.", [nameof(FechaNacimiento)]);
        if (CiudadId == Guid.Empty) yield return new("La ciudad es inválida.", [nameof(CiudadId)]);
        if (!UsuarioRules.IsHttpsUrl(FotoPerfilUrl)) yield return new("La foto debe usar una URL HTTPS.", [nameof(FotoPerfilUrl)]);
    }
}
