using Domain.Entities;
namespace Domain.Repositories
{
    public interface IDepartamentoRepository
    {
        Task AgregarDepartamentoAsync(string nombre, CancellationToken cancellationToken);
        Task<IEnumerable<Departamento>> ObtenerDepartamentosAsync(CancellationToken cancellationToken);
    }
}
