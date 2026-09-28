using Domain.Entities;
using Domain.Models;
namespace Domain.Repositories;
public interface INoticiaRepository
{
    Task<Guid> CrearAsync(Noticia noticia,CancellationToken ct);
    Task<IReadOnlyList<NoticiaDatos>> UltimasAsync(DateTime ahora,CancellationToken ct);
    Task<NoticiaDatos?> ObtenerAsync(Guid id,CancellationToken ct);
    Task<bool> ActualizarAsync(Guid id,NoticiaCambios cambios,DateTime ahora,CancellationToken ct);
}
