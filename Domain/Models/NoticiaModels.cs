namespace Domain.Models;
public sealed record NoticiaDatos(Guid Id,string Titulo,string? Resumen,string Contenido,string? ImagenPrincipalUrl,
    DateTime? FechaPublicacion,bool Publicado,bool Destacada,int CantidadVisualizaciones,string? Categoria,
    Guid? CiudadId,DateTime CreatedAt,DateTime UpdatedAt);
public sealed record NoticiaCambios(Cambio<string?> Titulo,Cambio<string?> Resumen,Cambio<string?> Contenido,
    Cambio<string?> ImagenPrincipalUrl,Cambio<DateTime?> FechaPublicacion,Cambio<bool> Publicado,
    Cambio<bool> Destacada,Cambio<string?> Categoria,Cambio<Guid?> CiudadId);
