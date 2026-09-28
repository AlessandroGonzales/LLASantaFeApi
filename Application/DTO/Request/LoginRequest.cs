using System.ComponentModel.DataAnnotations;
using Application.Validation;
namespace Application.DTO.Request;
public sealed class LoginRequest : IValidatableObject
{
    [Required, StringLength(254)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(72)] public string Password { get; set; } = string.Empty;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!UsuarioRules.PasswordFitsBcrypt(Password))
            yield return new("La contraseña no puede superar 72 bytes UTF-8.", [nameof(Password)]);
    }
}
