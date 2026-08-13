using Domain.Entities;

namespace Domain.Repositories
{
    public interface IRoleRepository
    {
        Task<Role> ObtenerRolIdPorNombreAsync(string nombre, CancellationToken cancellationToken);  
    }
}
