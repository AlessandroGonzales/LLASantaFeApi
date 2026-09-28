using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EfCiudad = Infrastructure.Persistence.Entities.Ciudade;
namespace Infrastructure.Repositories
{
    public class CiudadRepository : ICiudadRepository
    {
        private readonly LLASantaFeDbContext _db;
        public CiudadRepository(LLASantaFeDbContext db)
        {
            _db = db;
        }

        public static EfCiudad MapToEf(string nombre, Guid departamentoId) => new EfCiudad
        {
            Nombre = nombre,
            DepartamentoId = departamentoId
        };

        public async Task AgregarCiudadAsync(string nombre, Guid departamentoId, CancellationToken cancellationToken)
        {
            var efCiudad = MapToEf(nombre, departamentoId);
            await _db.Ciudades.AddAsync(efCiudad, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<Ciudade>> ObtenerCiudadesPorDepartamentoIdAsync(Guid departamentoId, CancellationToken cancellationToken)
        {
            var efCiudades = await _db.Ciudades
                .AsNoTracking()
                .Where(c => c.DepartamentoId == departamentoId)
                .Select(c => new Ciudade
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    DepartamentoId = c.DepartamentoId
                }).ToListAsync(cancellationToken);
            return efCiudades;
        }
    }
}
