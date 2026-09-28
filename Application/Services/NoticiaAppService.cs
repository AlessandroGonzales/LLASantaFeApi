using Application.DTO.Partial;
using Application.DTO.Request;
using Application.Interfaces;
using Application.Validation;
using Domain.Entities;
using Domain.Exceptions;
using Domain.Models;
using Domain.Repositories;
namespace Application.Services;
public sealed class NoticiaAppService(INoticiaRepository noticias,TimeProvider clock) : INoticiaAppService
{
    public Task<Guid> CrearAsync(NoticiaCrearRequest request,Guid autor,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        return noticias.CrearAsync(new Noticia
        {
            Titulo=request.Titulo.Trim(),Resumen=request.Resumen?.Trim(),Contenido=request.Contenido.Trim(),
            ImagenPrincipalUrl=request.ImagenPrincipalUrl,FechaPublicacion=request.FechaPublicacion?.UtcDateTime
                ?? (request.Publicado ? clock.GetUtcNow().UtcDateTime : null),
            Publicado=request.Publicado,Destacada=request.Destacada,Categoria=request.Categoria?.Trim(),
            CiudadId=request.CiudadId,CreatedBy=autor
        },ct);
    }
    public Task<IReadOnlyList<NoticiaDatos>> UltimasAsync(CancellationToken ct)=>noticias.UltimasAsync(clock.GetUtcNow().UtcDateTime,ct);
    public async Task<NoticiaDatos> ObtenerAsync(Guid id,CancellationToken ct)=>
        await noticias.ObtenerAsync(id,ct) ?? throw new BusinessException("not_found","La noticia no existe.");
    public async Task ActualizarAsync(Guid id,NoticiaPartial request,CancellationToken ct)
    {
        UsuarioRules.Validate(request);
        if (!await noticias.ActualizarAsync(id,request.ToChanges(),clock.GetUtcNow().UtcDateTime,ct))
            throw new BusinessException("not_found","La noticia no existe.");
    }
}
