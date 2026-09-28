using Domain.Models;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories;
public sealed class SolicitudesAfiliacionRepository(LLASantaFeDbContext db) : ISolicitudesAfiliacionRepository
{
    public async Task<IReadOnlyList<AfiliacionPendiente>> PendientesAsync(Guid? cursor, int limite, CancellationToken ct) =>
        await db.SolicitudesAfiliacions.AsNoTracking()
            .Where(s => s.Estado == "pendiente" && (!cursor.HasValue || s.Id.CompareTo(cursor.Value) > 0))
            .OrderBy(s => s.Id).Take(limite)
            .Select(s => new AfiliacionPendiente(s.Id, s.Usuario.Nombre, s.Usuario.Apellido, s.FechaSolicitud,
                s.Usuario.Activo && s.Usuario.DeletedAt == null)).ToListAsync(ct);
    public Task<AfiliacionContacto?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.SolicitudesAfiliacions.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new AfiliacionContacto(s.Id, s.Estado, s.FechaSolicitud, s.Observacion,
                s.UsuarioId, s.Usuario.Nombre, s.Usuario.Apellido, s.Usuario.Email, s.Usuario.Telefono,
                s.Usuario.Activo && s.Usuario.DeletedAt == null, s.AprobadaAt, s.AprobadaPor)).SingleOrDefaultAsync(ct);
    public Task<AfiliacionPropia?> ObtenerPropiaAsync(Guid usuario, CancellationToken ct) =>
        db.SolicitudesAfiliacions.AsNoTracking().Where(s => s.UsuarioId == usuario)
            .Select(s => new AfiliacionPropia(s.Id, s.Estado, s.FechaSolicitud, s.AprobadaAt)).SingleOrDefaultAsync(ct);
    public async Task<bool> AprobarAsync(Guid id, Guid admin, string? observacion, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.solicitudes_afiliacion s
            SET estado='aprobada', observacion={observacion}, aprobada_por={admin}, aprobada_at=now()
            WHERE s.id={id} AND s.estado IN ('pendiente','en_revision')
              AND EXISTS(SELECT 1 FROM public.usuarios u WHERE u.id=s.usuario_id AND u.activo AND u.deleted_at IS NULL)
              AND EXISTS(SELECT 1 FROM public.usuarios a JOIN public.roles r ON r.id=a.rol_id
                  WHERE a.id={admin} AND a.activo AND a.deleted_at IS NULL AND r.nombre IN ('Admin','SuperAdmin'))
            """,ct) == 1;
    public async Task<AfiliacionTotales> TotalesAsync(CancellationToken ct) =>
        await db.SolicitudesAfiliacions.AsNoTracking().GroupBy(s => 1)
            .Select(g => new AfiliacionTotales(g.LongCount(s => s.Estado == "aprobada"),g.LongCount(),
                g.LongCount(s => s.Estado == "pendiente"),g.LongCount(s => s.Estado == "en_revision"),
                g.LongCount(s => s.Estado == "rechazada"))).SingleOrDefaultAsync(ct)
            ?? new(0,0,0,0,0);
}
