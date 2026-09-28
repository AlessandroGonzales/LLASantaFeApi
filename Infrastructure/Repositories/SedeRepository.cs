using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using EfSede = Infrastructure.Persistence.Entities.Sede;


namespace Infrastructure.Repositories
{
    public class SedeRepository : ISedeRepository
    {
        private readonly LLASantaFeDbContext _db;
        public SedeRepository(LLASantaFeDbContext db)
        {
            _db = db;
        }

        public static EfSede MapToEf(Sede sede) => new EfSede
        {
            Nombre = sede.Nombre,
            Direccion = sede.Direccion,
            Telefono = sede.Telefono,
            Email = sede.Email,
            Horario = sede.Horario,
            Latitud = sede.Latitud,
            Longitud = sede.Longitud,
            ImagenUrl = sede.ImagenUrl,
            CiudadId = sede.CiudadId,

        };

        public async Task<IEnumerable<Sede>> ObtenerSedesAsync(CancellationToken cancellationToken)
        {
            var efSedes = await _db.Sedes
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return efSedes
                .Select(s => new Sede
            {
                Nombre = s.Nombre,
                Direccion = s.Direccion,
                Telefono = s.Telefono,
                Email = s.Email,
                Horario = s.Horario,
                Latitud = s.Latitud,
                Longitud = s.Longitud,
                ImagenUrl = s.ImagenUrl,
                CiudadId = s.CiudadId
            }).ToList();
        }

        public async Task ActualizarSedeAsync(Guid id, Domain.Models.SedeCambios sede, CancellationToken cancellationToken)
        {
            var affected = await _db.Sedes
                .Where(u => u.Id == id)
                .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Direccion, u => sede.Direccion ?? u.Direccion)
                .SetProperty(u => u.ImagenUrl, u => sede.ImagenUrl ?? u.ImagenUrl)
                .SetProperty(u => u.Latitud, u => sede.Latitud ?? u.Latitud)
                .SetProperty(u => u.Longitud, u => sede.Longitud ?? u.Longitud),
                cancellationToken);
            if (affected == 0)
                throw new Domain.Exceptions.BusinessException("not_found", "La sede no existe.");
        }

        public async Task AgregarSedeAsync(Sede sede, CancellationToken cancellationToken)
        {
            var efSede = MapToEf(sede);
            await _db.Sedes.AddAsync(efSede, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task EliminarSedeAsync(Guid id, CancellationToken cancellationToken)
        {
            var affected = await _db.Sedes.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
            if (affected == 0)
                throw new Domain.Exceptions.BusinessException("not_found", "La sede no existe.");
        }
    }
}
