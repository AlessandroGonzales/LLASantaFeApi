using System.ComponentModel.DataAnnotations;
using System.Text;
namespace Application.Validation;
public static class UsuarioRules
{
    public const string EmailPattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$";
    public const string PhonePattern = @"^\+?[0-9\s\-\(\)]{8,20}$";
    public static bool PasswordFitsBcrypt(string password) => Encoding.UTF8.GetByteCount(password) <= 72;
    public static bool IsHttpsUrl(string? url) => url is null ||
        (Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(parsed.UserInfo));
    public static void Validate(object request) =>
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
}
