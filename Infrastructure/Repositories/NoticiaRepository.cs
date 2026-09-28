using System.Linq.Expressions;
using Domain.Models;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EfNoticia=Infrastructure.Persistence.Entities.Noticia;
namespace Infrastructure.Repositories;
public sealed class NoticiaRepository(LLASantaFeDbContext db) : INoticiaRepository
{
    private static readonly Expression<Func<EfNoticia,NoticiaDatos>> Proyeccion=n=>new(n.Id,n.Titulo,n.Resumen,n.Contenido,
        n.ImagenPrincipalUrl,n.FechaPublicacion,n.Publicado,n.Destacada,n.CantidadVisualizaciones,n.Categoria,
        n.CiudadId,n.CreatedAt,n.UpdatedAt);
    public async Task<Guid> CrearAsync(Domain.Entities.Noticia n,CancellationToken ct)
    {
        var entity=new EfNoticia
        {
            Id=Guid.NewGuid(),Titulo=n.Titulo,Resumen=n.Resumen,Contenido=n.Contenido,ImagenPrincipalUrl=n.ImagenPrincipalUrl,
            FechaPublicacion=n.FechaPublicacion,Publicado=n.Publicado,Destacada=n.Destacada,Categoria=n.Categoria,
            CiudadId=n.CiudadId,CreatedBy=n.CreatedBy
        };
        db.Noticias.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.Id;
    }
    public async Task<IReadOnlyList<NoticiaDatos>> UltimasAsync(DateTime ahora,CancellationToken ct)=>
        await db.Noticias.AsNoTracking().Where(n=>n.Publicado && n.DeletedAt==null && n.FechaPublicacion!=null && n.FechaPublicacion<=ahora)
            .OrderByDescending(n=>n.FechaPublicacion).ThenByDescending(n=>n.Id).Take(6).Select(Proyeccion).ToListAsync(ct);
    public Task<NoticiaDatos?> ObtenerAsync(Guid id,CancellationToken ct)=>
        db.Noticias.AsNoTracking().Where(n=>n.Id==id && n.DeletedAt==null).Select(Proyeccion).SingleOrDefaultAsync(ct);
    public async Task<bool> ActualizarAsync(Guid id,NoticiaCambios c,DateTime ahora,CancellationToken ct)=>
        await db.Noticias.Where(n=>n.Id==id && n.DeletedAt==null).ExecuteUpdateAsync(set=>set
            .SetProperty(n=>n.Titulo,n=>c.Titulo.Incluido ? c.Titulo.Valor! : n.Titulo)
            .SetProperty(n=>n.Resumen,n=>c.Resumen.Incluido ? c.Resumen.Valor : n.Resumen)
            .SetProperty(n=>n.Contenido,n=>c.Contenido.Incluido ? c.Contenido.Valor! : n.Contenido)
            .SetProperty(n=>n.ImagenPrincipalUrl,n=>c.ImagenPrincipalUrl.Incluido ? c.ImagenPrincipalUrl.Valor : n.ImagenPrincipalUrl)
            .SetProperty(n=>n.FechaPublicacion,n=>(c.Publicado.Incluido ? c.Publicado.Valor : n.Publicado)
                ? (c.FechaPublicacion.Incluido ? c.FechaPublicacion.Valor : n.FechaPublicacion) ?? ahora
                : (c.FechaPublicacion.Incluido ? c.FechaPublicacion.Valor : n.FechaPublicacion))
            .SetProperty(n=>n.Publicado,n=>c.Publicado.Incluido ? c.Publicado.Valor : n.Publicado)
            .SetProperty(n=>n.Destacada,n=>c.Destacada.Incluido ? c.Destacada.Valor : n.Destacada)
            .SetProperty(n=>n.Categoria,n=>c.Categoria.Incluido ? c.Categoria.Valor : n.Categoria)
            .SetProperty(n=>n.CiudadId,n=>c.CiudadId.Incluido ? c.CiudadId.Valor : n.CiudadId),ct)==1;
}
