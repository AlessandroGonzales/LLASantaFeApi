using Application.DTO.Response;

namespace Application.Interfaces
{
    public interface ICiudadAppService
    {
        Task AgregarCiudadAsync(string nombre, Guid departamentoId, CancellationToken cancellationToken);
        Task<IEnumerable<CiudadResponse>> ObtenerCiudadesPorDepartamentoIdAsync(Guid departamentoId, CancellationToken cancellationToken);
    }
}
