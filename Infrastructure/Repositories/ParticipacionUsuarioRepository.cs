using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public sealed class ParticipacionUsuarioRepository(LLASantaFeDbContext db) : IParticipacionUsuarioRepository
{
    public Task<Encuesta?> ObtenerEncuestaAsync(Guid id, CancellationToken ct) =>
        db.Encuestas.AsNoTracking().Where(e => e.Id == id && e.DeletedAt == null)
            .Select(e => new Encuesta
            {
                Id = e.Id, Configuracion = e.Configuracion, Activa = e.Activa, PublicadaAt = e.PublicadaAt,
                FechaInicio = e.FechaInicio, FechaFin = e.FechaFin, DeletedAt = e.DeletedAt
            }).SingleOrDefaultAsync(ct);

    public async Task<bool> ResponderEncuestaAsync(Guid respuestaId, Guid usuarioId, Encuesta encuesta,
        string respuestas, DateOnly fecha, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.encuesta_respuestas (id, encuesta_id, usuario_id, respuestas)
            SELECT {respuestaId}, e.id, {usuarioId}, CAST({respuestas} AS jsonb)
            FROM public.encuestas e
            WHERE e.id = {encuesta.Id} AND e.activa AND e.publicada_at IS NOT NULL AND e.deleted_at IS NULL
              AND (e.fecha_inicio IS NULL OR e.fecha_inicio <= {fecha})
              AND (e.fecha_fin IS NULL OR e.fecha_fin >= {fecha})
              AND e.configuracion = CAST({encuesta.Configuracion} AS jsonb)
              AND EXISTS (SELECT 1 FROM public.usuarios u WHERE u.id = {usuarioId} AND u.activo AND u.deleted_at IS NULL)
            FOR SHARE OF e
            """, ct) == 1;

    public async Task<bool> SolicitarAfiliacionAsync(Guid solicitudId, Guid usuarioId, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.solicitudes_afiliacion (id, usuario_id, estado)
            SELECT {solicitudId}, u.id, 'pendiente'
            FROM public.usuarios u WHERE u.id = {usuarioId} AND u.activo AND u.deleted_at IS NULL
            """, ct) == 1;

    public async Task<bool> EnviarSolicitudAsync(Guid solicitudId, Guid usuarioId, string motivo, string mensaje, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.solicitudes_ciudadanas (id, usuario_id, motivo, mensaje, aceptacion)
            SELECT {solicitudId}, u.id, {motivo}, {mensaje}, false
            FROM public.usuarios u WHERE u.id = {usuarioId} AND u.activo AND u.deleted_at IS NULL
            """, ct) == 1;
}
