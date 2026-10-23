using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class SolicitudesAfiliacionAppService(ISolicitudesAfiliacionRepository afiliaciones, ICorreoWakeSignal correoWake) : ISolicitudesAfiliacionAppService
{
    public async Task<PaginaAfiliaciones> PendientesAsync(string estado, Guid? cursor, int limite, CancellationToken ct)
    {
        if (limite is < 1 or > 50 || estado is not ("pendiente" or "en_revision"))
            throw new BusinessException("validation", "Indicá un límite entre 1 y 50 y estado pendiente o en_revision.");
        var rows = await afiliaciones.PendientesAsync(estado, cursor, limite + 1, ct);
        return new(rows.Take(limite).ToArray(), rows.Count > limite ? rows[limite - 1].Id : null);
    }
    public async Task<AfiliacionContacto> ObtenerAsync(Guid id, CancellationToken ct) =>
        await afiliaciones.ObtenerAsync(id, ct) ?? throw new BusinessException("not_found", "Solicitud de afiliación no disponible.");
    public async Task<AfiliacionPropia> ObtenerPropiaAsync(Guid usuario, CancellationToken ct) =>
        await afiliaciones.ObtenerPropiaAsync(usuario, ct) ?? throw new BusinessException("not_found", "Todavía no registraste una solicitud de afiliación.");
    public async Task AprobarAsync(Guid id, Guid admin, AfiliacionAprobarRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (request.Observacion?.Contains('\0') == true) throw new BusinessException("validation", "La observación no admite caracteres NUL.");
        var observacion=string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim();
        if (!await afiliaciones.AprobarAsync(id, admin, observacion, ct)) await ResolverErrorAsync(id,ct);
        correoWake.Notify();
    }
    public async Task RechazarAsync(Guid id, Guid admin, AfiliacionRechazarRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        var motivo=request.Motivo.Trim();
        if (motivo.Length<10 || motivo.Contains('\0')) throw new BusinessException("validation", "Indicá un motivo de rechazo de al menos 10 caracteres, sin NUL.");
        if (!await afiliaciones.RechazarAsync(id,admin,motivo,ct)) await ResolverErrorAsync(id,ct);
    }
    private async Task ResolverErrorAsync(Guid id,CancellationToken ct)
    {
        if (!await afiliaciones.ExisteAsync(id,ct)) throw new BusinessException("not_found", "Solicitud de afiliación no disponible.");
        throw new BusinessException("conflict", "La solicitud ya fue resuelta. Volvé a consultar su estado.");
    }
    public Task<AfiliacionTotales> TotalesAsync(CancellationToken ct) => afiliaciones.TotalesAsync(ct);
}
