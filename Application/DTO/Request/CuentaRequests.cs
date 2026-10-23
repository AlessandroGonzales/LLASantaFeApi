using System.ComponentModel.DataAnnotations;
using Application.Validation;

namespace Application.DTO.Request;

public sealed class CuentaEmailRequest
{
    [Required,StringLength(254),RegularExpression(UsuarioRules.EmailPattern)] public string Email { get; set; }="";
}
public sealed class EmailConfirmarRequest : IValidatableObject
{
    [Required,StringLength(254),RegularExpression(UsuarioRules.EmailPattern)] public string Email { get; set; }="";
    [Required,StringLength(72)] public string Password { get; set; }="";
    [Required,StringLength(80,MinimumLength=80)] public string Codigo { get; set; }="";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!UsuarioRules.PasswordFitsBcrypt(Password)) yield return new("La contraseña no puede superar 72 bytes UTF-8.",[nameof(Password)]);
    }
}
public sealed class PasswordRestablecerRequest : IValidatableObject
{
    [Required,StringLength(80,MinimumLength=80)] public string Codigo { get; set; }="";
    [Required,StringLength(72,MinimumLength=15)] public string NuevaPassword { get; set; }="";
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!UsuarioRules.PasswordFitsBcrypt(NuevaPassword)) yield return new("La contraseña no puede superar 72 bytes UTF-8.",[nameof(NuevaPassword)]);
    }
}
