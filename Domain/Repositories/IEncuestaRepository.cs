namespace Domain.Repositories
{
    public interface IEncuestaRepository
    {
        Task<IReadOnlyList<Domain.Models.EncuestaResumen>> ListarAsync(Guid usuarioId, bool administracion, DateOnly fecha, Guid? despuesDe, int limite, CancellationToken ct);
        Task<Domain.Entities.Encuesta?> ObtenerAsync(Guid id, bool administracion, DateOnly fecha, CancellationToken ct);
        Task<Guid> CrearAsync(Domain.Models.EncuestaEdicion datos, Guid autor, CancellationToken ct);
        Task<bool> EditarAsync(Guid id, long revision, Domain.Models.EncuestaEdicion datos, CancellationToken ct);
        Task<bool> CambiarEstadoAsync(Guid id, long revision, bool publicar, DateOnly fecha, CancellationToken ct);
        Task<IReadOnlyList<Domain.Models.RespuestaEncuestaDatos>> RespuestasAsync(Guid id, Guid? despuesDe, int limite, CancellationToken ct);
    }
}
