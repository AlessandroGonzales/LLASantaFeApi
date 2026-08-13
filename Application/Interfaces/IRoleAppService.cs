using Application.DTO.Response;

namespace Application.Interfaces
{
    public interface IRoleAppService
    {
        Task<RoleResponse> ObtenerRolIdPorNombreAsync(string nombre, CancellationToken cancellationToken);
    }
}
