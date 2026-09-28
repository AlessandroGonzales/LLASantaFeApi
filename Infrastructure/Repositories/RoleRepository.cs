using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly LLASantaFeDbContext _db;
        public RoleRepository(LLASantaFeDbContext db)
        {
            _db = db;
        }

        public async Task<Role> ObtenerRolIdPorNombreAsync(string nombre, CancellationToken cancellationToken)
        {
            var rol = await _db.Roles
                .AsNoTracking()
                .Where(r => r.Nombre == nombre)
                .Select( r =>  new Role{
                    Nombre = r.Nombre, Id = r.Id })
                .FirstOrDefaultAsync(cancellationToken);
            return rol ?? throw new InvalidOperationException("No está configurado el rol requerido.");
        }
    }
}
