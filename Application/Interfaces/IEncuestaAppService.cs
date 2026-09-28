using Application.DTO.Request;
using Application.DTO.Response;
using Domain.Models;
namespace Application.Interfaces;
public interface IEncuestaAppService
{
    Task<PaginaEncuesta<EncuestaResumen>> ListarAsync(Guid usuario, bool admin, Guid? cursor, int limite, CancellationToken ct);
    Task<EncuestaDetalleResponse> ObtenerAsync(Guid id, bool admin, CancellationToken ct);
    Task<Guid> CrearAsync(EncuestaCrearRequest request, Guid autor, CancellationToken ct);
    Task EditarAsync(Guid id, EncuestaEditarRequest request, CancellationToken ct);
    Task CambiarEstadoAsync(Guid id, EncuestaEstadoRequest request, bool publicar, CancellationToken ct);
    Task<PaginaEncuesta<RespuestaEncuestaResponse>> RespuestasAsync(Guid id, Guid? cursor, int limite, CancellationToken ct);
}
