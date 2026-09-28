using Application.DTO.Partial;
using Application.DTO.Request;
using Domain.Models;
namespace Application.Interfaces;
public interface INoticiaAppService
{
    Task<Guid> CrearAsync(NoticiaCrearRequest request,Guid autor,CancellationToken ct);
    Task<IReadOnlyList<NoticiaDatos>> UltimasAsync(CancellationToken ct);
    Task<NoticiaDatos> ObtenerAsync(Guid id,CancellationToken ct);
    Task ActualizarAsync(Guid id,NoticiaPartial request,CancellationToken ct);
}
