using Domain.Repositories;
using Infrastructure.Persistence;
using EfDepartamento = Infrastructure.Persistence.Entities.Departamento;

namespace Infrastructure.Repositories
{
    public class DepartamentoRepository : IDepartamentoRepository
    {
        private readonly LLASantaFeDbContext _db;
        public DepartamentoRepository(LLASantaFeDbContext db)
        {
            _db = db;
        }

        private static EfDepartamento MapToEf(string nombre) => new EfDepartamento
        {
            Nombre = nombre,
        };

        public async Task AgregarDepartamentoAsync(string nombre, CancellationToken cancellationToken)
        {
            var efDepartamento = MapToEf(nombre);
            await _db.Departamentos.AddAsync(efDepartamento, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
