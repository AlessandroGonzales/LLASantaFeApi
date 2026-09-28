namespace Domain.Models;
public readonly record struct Cambio<T>(bool Incluido, T Valor);
public sealed record UsuarioCambios(Cambio<string?> Nombre, Cambio<string?> Apellido,
    Cambio<string?> Telefono, Cambio<string?> FotoPerfilUrl, Cambio<Guid?> CiudadId);
