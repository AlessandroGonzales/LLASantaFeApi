using Application.DTO.Response;
using Application.Interfaces;
using Domain.Repositories;

namespace Application.Services
{
    public class RoleAppService : IRoleAppService
    {
        private readonly IRoleRepository _roleRepository;
        public RoleAppService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<RoleResponse> ObtenerRolIdPorNombreAsync(string nombre, CancellationToken cancellationToken)
        {
           var role = await _roleRepository.ObtenerRolIdPorNombreAsync(nombre, cancellationToken);
           return new RoleResponse
            {
                RoleNombre = role.Nombre,
                RoleId = role.Id
            };

        }
    }
}
