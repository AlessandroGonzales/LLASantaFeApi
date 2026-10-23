using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace Presentation.Security;

public sealed class CatalogCacheOptions
{
    [Range(1, 32)] public int SizeMiB { get; set; } = 4;
    [Range(1024, 262144)] public int MaxBodyBytes { get; set; } = 131072;
    [Range(1, 900)] public int DepartmentsSeconds { get; set; } = 300;
    [Range(1, 900)] public int CitiesSeconds { get; set; } = 300;
    [Range(1, 300)] public int SedesSeconds { get; set; } = 60;
}

public sealed class CatalogCacheRevision
{
    private long geography,sedes;
    public long Current(string tag) => tag==PublicCatalogCache.GeographyTag ? Interlocked.Read(ref geography) : Interlocked.Read(ref sedes);
    public void Changed(string tag)
    {
        if (tag==PublicCatalogCache.GeographyTag) Interlocked.Increment(ref geography);
        else Interlocked.Increment(ref sedes);
    }
}

public static class PublicCatalogCache
{
    public const string Departments="catalogo_departamentos", Cities="catalogo_ciudades", Sedes="catalogo_sedes";
    public const string GeographyTag="catalogo_geografia", SedesTag="catalogo_sedes";
    public static IServiceCollection AddPublicCatalogCache(this IServiceCollection services,IConfiguration configuration)
    {
        var settings=configuration.GetSection("CatalogCache").Get<CatalogCacheOptions>() ?? new();
        Validator.ValidateObject(settings,new ValidationContext(settings),true);
        var revisions=new CatalogCacheRevision();
        services.AddSingleton(revisions);
        services.AddOutputCache(options =>
        {
            options.SizeLimit=settings.SizeMiB*1024L*1024;
            options.MaximumBodySize=settings.MaxBodyBytes;
            options.AddPolicy(Departments,new PublicCatalogPolicy(settings.DepartmentsSeconds,GeographyTag,false,revisions));
            options.AddPolicy(Cities,new PublicCatalogPolicy(settings.CitiesSeconds,GeographyTag,true,revisions));
            options.AddPolicy(Sedes,new PublicCatalogPolicy(settings.SedesSeconds,SedesTag,false,revisions));
        });
        return services;
    }
}

// Solo catálogos anónimos explícitos; no cachea identidad, cookies ni errores.
public sealed class PublicCatalogPolicy(int seconds,string tag,bool departmentQuery,CatalogCacheRevision revisions) : IOutputCachePolicy
{
    public ValueTask CacheRequestAsync(OutputCacheContext context,CancellationToken ct)
    {
        var http=context.HttpContext;
        var request=http.Request;
        var eligible=HttpMethods.IsGet(request.Method) &&
            http.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null &&
            !request.Headers.ContainsKey("Authorization") && !request.Headers.ContainsKey("Cookie") &&
            http.User.Identity?.IsAuthenticated!=true;
        if (departmentQuery)
        {
            eligible=eligible && request.Query.Count==1 && request.Query.TryGetValue("departamentoId",out var values) &&
                values.Count==1 && Guid.TryParse(values[0],out var department) && department!=Guid.Empty;
            if (eligible) context.CacheVaryByRules.VaryByValues["departamentoId"]=Guid.Parse(request.Query["departamentoId"]!).ToString("D");
        }
        else eligible=eligible && request.Query.Count==0;
        context.EnableOutputCaching=true;
        context.AllowCacheLookup=eligible;
        context.AllowCacheStorage=eligible;
        context.AllowLocking=true;
        context.ResponseExpirationTimeSpan=TimeSpan.FromSeconds(seconds);
        context.CacheVaryByRules.QueryKeys=StringValues.Empty;
        // Las cabeceras CORS de un origen nunca se reutilizan para otro.
        context.CacheVaryByRules.HeaderNames="Origin";
        // Una consulta empezada antes del cambio no puede repoblar la clave vigente con datos antiguos.
        context.CacheVaryByRules.CacheKeyPrefix=$"{tag}:{revisions.Current(tag)}";
        context.Tags.Add(tag);
        http.Response.Headers.CacheControl=eligible ? $"public, max-age={Math.Min(seconds,30)}" : "no-store";
        if (eligible && !http.Response.Headers.Vary.ToString().Split(',').Any(value=>value.Trim().Equals("Origin",StringComparison.OrdinalIgnoreCase)))
            http.Response.Headers.Append("Vary","Origin");
        return ValueTask.CompletedTask;
    }
    public ValueTask ServeFromCacheAsync(OutputCacheContext context,CancellationToken ct) => ValueTask.CompletedTask;
    public ValueTask ServeResponseAsync(OutputCacheContext context,CancellationToken ct)
    {
        var response=context.HttpContext.Response;
        if (response.StatusCode!=StatusCodes.Status200OK || response.Headers.ContainsKey("Set-Cookie") ||
            response.Headers.CacheControl.ToString().Contains("no-store",StringComparison.OrdinalIgnoreCase))
            context.AllowCacheStorage=false;
        return ValueTask.CompletedTask;
    }
}
