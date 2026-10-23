using Application.DTO.Response;

namespace Application.Interfaces
{
    public interface IDepartamentoAppService
    {
        Task AgregarDepartamentoAsync (string nombre, CancellationToken cancellationToken);
        Task<IEnumerable<DepartamentoResponse>> ObtenerDepartamentosAsync(CancellationToken cancellationToken);
    }
}
