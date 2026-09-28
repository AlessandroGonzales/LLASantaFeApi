
namespace Domain.Repositories
{
    public interface IDepartamentoRepository
    {
        Task AgregarDepartamentoAsync(string nombre, CancellationToken cancellationToken);
    }
}
