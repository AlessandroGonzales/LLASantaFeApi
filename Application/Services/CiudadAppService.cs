using Application.DTO.Response;
using Application.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class CiudadAppService : ICiudadAppService
    {
        private readonly ICiudadRepository _repo;
        public CiudadAppService(ICiudadRepository repo)
        {
            _repo = repo;
        }

        public async Task AgregarCiudadAsync(string nombre, Guid departamentoId, CancellationToken cancellationToken)
        {
            await _repo.AgregarCiudadAsync(nombre, departamentoId, cancellationToken);
        }

        public async Task<IEnumerable<CiudadResponse>> ObtenerCiudadesPorDepartamentoIdAsync(Guid departamentoId, CancellationToken cancellationToken)
        {
            var ciudades = await _repo.ObtenerCiudadesPorDepartamentoIdAsync(departamentoId, cancellationToken);
            return ciudades.Select(c => new CiudadResponse
            {
                IdCiudad = c.Id,
                Nombre = c.Nombre,
                DepartamentoId = c.DepartamentoId
            });
        }
    }
}
