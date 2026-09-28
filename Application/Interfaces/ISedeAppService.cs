using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;

namespace Application.Interfaces
{
    public interface ISedeAppService
    {
        Task AgregarSedeAsync(SedeRequest sedeRequest, CancellationToken cancellationToken);
        Task <IEnumerable<SedeResponse>> MostrarSedesAsync (CancellationToken cancellationToken);
        Task ActualizarSedeAsync(Guid id, SedePartial sedePartial, CancellationToken cancellationToken);
        Task EliminarSedeAsync(Guid id, CancellationToken cancellationToken);
    }
}
