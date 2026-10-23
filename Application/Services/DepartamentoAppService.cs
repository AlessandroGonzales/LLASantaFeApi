using Application.Interfaces;
using Application.DTO.Response;
using Application.Validation;
using Domain.Repositories;

namespace Application.Services
{
    public class DepartamentoAppService : IDepartamentoAppService
    {
        private readonly IDepartamentoRepository _repo;
        public DepartamentoAppService(IDepartamentoRepository repo)
        {
            _repo = repo;
        }

        public async Task AgregarDepartamentoAsync(string nombre, CancellationToken cancellationToken)
        {
            await _repo.AgregarDepartamentoAsync(GeografiaRules.Nombre(nombre), cancellationToken);
        }

        public async Task<IEnumerable<DepartamentoResponse>> ObtenerDepartamentosAsync(CancellationToken cancellationToken)
        {
            var departamentos = await _repo.ObtenerDepartamentosAsync(cancellationToken);
            return departamentos.Select(d => new DepartamentoResponse { IdDepartamento = d.Id, Nombre = d.Nombre });
        }

    }

}
