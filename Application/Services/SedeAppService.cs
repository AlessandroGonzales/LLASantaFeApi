using Application.DTO.Partial;
using Application.DTO.Request;
using Application.DTO.Response;
using Application.Interfaces;
using Domain.Entities;
using Domain.Repositories;

namespace Application.Services
{
    public class SedeAppService : ISedeAppService
    {
        private readonly ISedeRepository _repo;
        public SedeAppService(ISedeRepository repo)
        {
            _repo = repo;
        }

        public async Task AgregarSedeAsync(SedeRequest sedeRequest, CancellationToken cancellationToken)
        {
            await _repo.AgregarSedeAsync(new Sede
            {
                Nombre = sedeRequest.Nombre,
                Direccion = sedeRequest.Direccion,
                Telefono = sedeRequest.Telefono,
                Email = sedeRequest.Email,
                Horario = sedeRequest.Horario,
                Latitud = sedeRequest.Latitud,
                Longitud = sedeRequest.Longitud,
                ImagenUrl = sedeRequest.ImagenUrl,
                CiudadId = sedeRequest.CiudadId,
            }, cancellationToken);
        }

        public async Task<IEnumerable<SedeResponse>> MostrarSedesAsync( CancellationToken cancellationToken)
        {
            var sedes = await _repo.ObtenerSedesAsync(cancellationToken);
            return sedes.Select(s => new SedeResponse
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
            });
        }

        public async Task ActualizarSedeAsync(Guid id, SedePartial sedePartial, CancellationToken cancellationToken)
        {
            await _repo.ActualizarSedeAsync(id, new Domain.Models.SedeCambios
            {
                Direccion = sedePartial.Direccion,
                Latitud = sedePartial.Latitud,
                Longitud = sedePartial.Longitud,
                ImagenUrl = sedePartial.ImagenUrl
            }, cancellationToken);
        }

        public async Task EliminarSedeAsync(Guid id, CancellationToken cancellationToken)
        {
            await _repo.EliminarSedeAsync(id, cancellationToken);
        }
    }
}
