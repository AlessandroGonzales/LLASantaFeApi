using Application.Authentication;
using Application.Interfaces;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Registro de Servicios de Aplicación (Casos de Uso)
            services.AddScoped<ICiudadAppService, CiudadAppService>();
            services.AddScoped<IDepartamentoAppService, DepartamentoAppService>();
            services.AddScoped<IEncuestaAppService, EncuestaAppService>();
            services.AddScoped<IEncuestaRespuestaAppService, EncuestaRespuestaAppService>();
            services.AddScoped<IEventoAppService, EventoAppService>();
            services.AddScoped<INoticiaAppService, NoticiaAppService>();
            services.AddScoped<INotificacionAppService, NotificacionAppService>();
            services.AddScoped<IPropuestaAppService, PropuestaAppService>();
            services.AddScoped<IRepresentanteAppService, RepresentanteAppService>();
            services.AddScoped<IRoleAppService, RoleAppService>();
            services.AddScoped<ISedeAppService, SedeAppService>();
            services.AddScoped<ISolicitudesAfiliacionAppService, SolicitudesAfiliacionAppService>();
            services.AddScoped<ISolicitudesCiudadanaAppService, SolicitudesCiudadanaAppService>();
            services.AddScoped<IUsuarioAppService, UsuarioAppService>();

            services.AddSingleton(TimeProvider.System);

            return services;
        }
    }
}
