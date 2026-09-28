using Application.Interfaces;
using Domain.Entities;
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
            await _repo.AgregarDepartamentoAsync(nombre, cancellationToken);
        }

    }

}
