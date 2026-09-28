using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class SolicitudesAfiliacionAppService(ISolicitudesAfiliacionRepository afiliaciones) : ISolicitudesAfiliacionAppService
{
    public async Task<PaginaAfiliaciones> PendientesAsync(Guid? cursor, int limite, CancellationToken ct)
    {
        if (limite is < 1 or > 50) throw new BusinessException("validation", "El límite debe estar entre 1 y 50.");
        var rows = await afiliaciones.PendientesAsync(cursor, limite + 1, ct);
        return new(rows.Take(limite).ToArray(), rows.Count > limite ? rows[limite - 1].Id : null);
    }
    public async Task<AfiliacionContacto> ObtenerAsync(Guid id, CancellationToken ct) =>
        await afiliaciones.ObtenerAsync(id, ct) ?? throw new BusinessException("not_found", "Solicitud de afiliación no disponible.");
    public async Task<AfiliacionPropia> ObtenerPropiaAsync(Guid usuario, CancellationToken ct) =>
        await afiliaciones.ObtenerPropiaAsync(usuario, ct) ?? throw new BusinessException("not_found", "Todavía no registraste una solicitud de afiliación.");
    public async Task AprobarAsync(Guid id, Guid admin, AfiliacionAprobarRequest request, CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        await ObtenerAsync(id, ct);
        if (!await afiliaciones.AprobarAsync(id, admin, request.Observacion?.Trim(), ct))
            throw new BusinessException("conflict", "La solicitud ya fue resuelta o la cuenta está inactiva. Volvé a consultar su estado.");
    }
    public Task<AfiliacionTotales> TotalesAsync(CancellationToken ct) => afiliaciones.TotalesAsync(ct);
}
