using Domain.Entities;

namespace Domain.Repositories
{
    public interface ICiudadRepository
    {
        Task AgregarCiudadAsync(string nombre, Guid departamentoId, CancellationToken cancellationToken);
        Task<IEnumerable<Ciudade>> ObtenerCiudadesPorDepartamentoIdAsync(Guid departamentoId, CancellationToken cancellationToken);
    }
}
