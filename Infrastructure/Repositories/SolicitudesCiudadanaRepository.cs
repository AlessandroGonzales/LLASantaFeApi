using Domain.Models;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public sealed class SolicitudesCiudadanaRepository(LLASantaFeDbContext db) : ISolicitudesCiudadanaRepository
{
    public async Task<IReadOnlyList<SolicitudVista>> ListarAsync(Guid usuario, bool admin, string? estado, Guid? cursor, int limite, CancellationToken ct) =>
        await db.Database.SqlQuery<SolicitudVista>($"""
            SELECT s.id AS "Id", s.motivo AS "Motivo", s.estado AS "Estado", s.revision AS "Revision",
                s.created_at AS "CreatedAt", EXISTS(SELECT 1 FROM public.solicitud_pdf p WHERE p.solicitud_id=s.id) AS "TienePdf"
            FROM public.solicitudes_ciudadanas s
            WHERE ({admin} OR s.usuario_id={usuario}) AND ({estado}::text IS NULL OR s.estado={estado})
              AND ({cursor}::uuid IS NULL OR s.id>{cursor})
            ORDER BY s.id LIMIT {limite}
            """).ToListAsync(ct);
    public Task<SolicitudDetalle?> ObtenerAsync(Guid id, Guid usuario, bool admin, CancellationToken ct) =>
        db.Database.SqlQuery<SolicitudDetalle>($"""
            SELECT s.id AS "Id",s.motivo AS "Motivo",s.mensaje AS "Mensaje",s.estado AS "Estado",s.respuesta AS "Respuesta",
                s.revision AS "Revision",s.created_at AS "CreatedAt",s.updated_at AS "UpdatedAt",
                EXISTS(SELECT 1 FROM public.solicitud_pdf p WHERE p.solicitud_id=s.id) AS "TienePdf"
            FROM public.solicitudes_ciudadanas s WHERE s.id={id} AND ({admin} OR s.usuario_id={usuario})
            """).SingleOrDefaultAsync(ct);
    public async Task<bool> GestionarAsync(Guid id, Guid admin, long revision, string estado, string? respuesta, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.solicitudes_ciudadanas SET estado={estado},respuesta={respuesta},gestionado_por={admin}
            WHERE id={id} AND revision={revision} AND estado IN ('pendiente','en_revision')
            """,ct) == 1;
    public Task<Guid> ReservarPdfAsync(Guid id, Guid usuario, CancellationToken ct) =>
        db.Database.SqlQuery<Guid>($"SELECT public.reservar_solicitud_pdf({id},{usuario}) AS \"Value\"").SingleAsync(ct);
    public async Task GuardarPdfAsync(Guid id, Guid usuario, Guid intento, byte[] contenido, string hash, CancellationToken ct) =>
        _ = await db.Database.SqlQuery<int>($"SELECT public.guardar_solicitud_pdf({id},{usuario},{intento},{contenido},{hash}) AS \"Value\"").SingleAsync(ct);
    public Task<SolicitudPdfDatos?> ObtenerPdfAsync(Guid id, Guid usuario, bool admin, CancellationToken ct) =>
        db.Database.SqlQuery<SolicitudPdfDatos>($"""
            SELECT p.contenido AS "Contenido" FROM public.solicitud_pdf p
            WHERE p.solicitud_id={id} AND ({admin} OR p.usuario_id={usuario})
            """).SingleOrDefaultAsync(ct);
}
