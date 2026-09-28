using Domain.Entities;

namespace Domain.Repositories
{
    public interface ISedeRepository
    {
        Task AgregarSedeAsync(Sede sede, CancellationToken cancellationToken);
        Task<IEnumerable<Sede>> ObtenerSedesAsync( CancellationToken cancellationToken);
        Task ActualizarSedeAsync(Guid id, Domain.Models.SedeCambios sede, CancellationToken cancellationToken);
        Task EliminarSedeAsync(Guid id, CancellationToken cancellationToken);
    }
}
