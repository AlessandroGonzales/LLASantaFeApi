using Domain.Models;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EfEncuesta = Infrastructure.Persistence.Entities.Encuesta;
namespace Infrastructure.Repositories;

public sealed class EncuestaRepository(LLASantaFeDbContext db) : IEncuestaRepository
{
    private IQueryable<EfEncuesta> Visibles(bool admin, DateOnly fecha) => db.Encuestas.AsNoTracking()
        .Where(e => e.DeletedAt == null && (admin || (e.PublicadaAt != null && e.Activa &&
            (e.FechaInicio == null || e.FechaInicio <= fecha) && (e.FechaFin == null || e.FechaFin >= fecha))));

    public async Task<IReadOnlyList<EncuestaResumen>> ListarAsync(Guid usuarioId, bool administracion,
        DateOnly fecha, Guid? despuesDe, int limite, CancellationToken ct) =>
        await Visibles(administracion, fecha).Where(e => !despuesDe.HasValue || e.Id.CompareTo(despuesDe.Value) > 0)
            .OrderBy(e => e.Id).Take(limite).Select(e => new EncuestaResumen(e.Id, e.Titulo, e.Descripcion,
                e.Tipo, e.FechaInicio, e.FechaFin, e.Activa, e.PublicadaAt, e.Revision,
                e.EncuestaRespuesta.Any(r => r.UsuarioId == usuarioId))).ToListAsync(ct);

    public Task<Domain.Entities.Encuesta?> ObtenerAsync(Guid id, bool administracion, DateOnly fecha, CancellationToken ct) =>
        Visibles(administracion, fecha).Where(e => e.Id == id).Select(e => new Domain.Entities.Encuesta
        {
            Id = e.Id, Titulo = e.Titulo, Descripcion = e.Descripcion, Tipo = e.Tipo,
            FechaInicio = e.FechaInicio, FechaFin = e.FechaFin, Activa = e.Activa,
            PublicadaAt = e.PublicadaAt, Revision = e.Revision, CiudadId = e.CiudadId,
            DepartamentoId = e.DepartamentoId, Configuracion = e.Configuracion
        }).SingleOrDefaultAsync(ct);

    public async Task<Guid> CrearAsync(EncuestaEdicion d, Guid autor, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        // INSERT explícito evita que el default histórico activa=true publique un borrador.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.encuestas (id,titulo,descripcion,tipo,fecha_inicio,fecha_fin,
                configuracion,ciudad_id,departamento_id,created_by,activa)
            VALUES ({id},{d.Titulo},{d.Descripcion},{d.Tipo},{d.FechaInicio},{d.FechaFin},
                CAST({d.Configuracion} AS jsonb),{d.CiudadId},{d.DepartamentoId},{autor},false)
            """, ct);
        return id;
    }
    public async Task<bool> EditarAsync(Guid id, long revision, EncuestaEdicion d, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.encuestas SET titulo={d.Titulo},descripcion={d.Descripcion},tipo={d.Tipo},
                fecha_inicio={d.FechaInicio},fecha_fin={d.FechaFin},configuracion=CAST({d.Configuracion} AS jsonb),
                ciudad_id={d.CiudadId},departamento_id={d.DepartamentoId}
            WHERE id={id} AND revision={revision} AND publicada_at IS NULL AND deleted_at IS NULL
            """, ct) == 1;

    public async Task<bool> CambiarEstadoAsync(Guid id, long revision, bool publicar, DateOnly fecha, CancellationToken ct)
    {
        if (publicar)
            return await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.encuestas SET publicada_at=now(),activa=true
                WHERE id={id} AND revision={revision} AND publicada_at IS NULL AND deleted_at IS NULL
                    AND (fecha_fin IS NULL OR fecha_fin >= {fecha})
                """, ct) == 1;
        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.encuestas SET activa=false
            WHERE id={id} AND revision={revision} AND publicada_at IS NOT NULL AND activa AND deleted_at IS NULL
            """, ct) == 1;
    }
    public async Task<IReadOnlyList<RespuestaEncuestaDatos>> RespuestasAsync(Guid id, Guid? despuesDe, int limite, CancellationToken ct) =>
        await db.EncuestaRespuestas.AsNoTracking().Where(r => r.EncuestaId == id &&
                (!despuesDe.HasValue || r.Id.CompareTo(despuesDe.Value) > 0))
            .OrderBy(r => r.Id).Take(limite)
            .Select(r => new RespuestaEncuestaDatos(r.Id, r.Respuestas, r.CreatedAt)).ToListAsync(ct);
}
