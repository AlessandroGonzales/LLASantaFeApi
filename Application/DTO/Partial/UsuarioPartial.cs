using System.ComponentModel.DataAnnotations;
using Application.Validation;
using Domain.Models;
namespace Application.DTO.Partial;
public sealed class UsuarioPartial : IValidatableObject
{
    private string? nombre, apellido, telefono, fotoPerfilUrl;
    private Guid? ciudadId;
    private readonly HashSet<string> campos = [];
    [StringLength(100)]
    public string? Nombre { get => nombre; set { nombre = value; campos.Add(nameof(Nombre)); } }
    [StringLength(100)]
    public string? Apellido { get => apellido; set { apellido = value; campos.Add(nameof(Apellido)); } }
    [RegularExpression(UsuarioRules.PhonePattern)]
    public string? Telefono { get => telefono; set { telefono = value; campos.Add(nameof(Telefono)); } }
    [StringLength(2048)]
    public string? FotoPerfilUrl { get => fotoPerfilUrl; set { fotoPerfilUrl = value; campos.Add(nameof(FotoPerfilUrl)); } }
    public Guid? CiudadId { get => ciudadId; set { ciudadId = value; campos.Add(nameof(CiudadId)); } }
    public UsuarioCambios ToChanges() => new(
        new(campos.Contains(nameof(Nombre)), Nombre?.Trim()),
        new(campos.Contains(nameof(Apellido)), Apellido?.Trim()),
        new(campos.Contains(nameof(Telefono)), Telefono?.Trim()),
        new(campos.Contains(nameof(FotoPerfilUrl)), FotoPerfilUrl),
        new(campos.Contains(nameof(CiudadId)), CiudadId));
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (campos.Count == 0) yield return new("Incluí al menos un campo editable.");
        if (campos.Contains(nameof(Nombre)) && string.IsNullOrWhiteSpace(Nombre)) yield return new("El nombre no puede estar vacío.", [nameof(Nombre)]);
        if (campos.Contains(nameof(Apellido)) && string.IsNullOrWhiteSpace(Apellido)) yield return new("El apellido no puede estar vacío.", [nameof(Apellido)]);
        if (Telefono is not null && string.IsNullOrWhiteSpace(Telefono)) yield return new("Usá null para quitar el teléfono.", [nameof(Telefono)]);
        if (CiudadId == Guid.Empty) yield return new("La ciudad es inválida.", [nameof(CiudadId)]);
        if (!UsuarioRules.IsHttpsUrl(FotoPerfilUrl)) yield return new("La foto debe usar una URL HTTPS.", [nameof(FotoPerfilUrl)]);
    }
}
