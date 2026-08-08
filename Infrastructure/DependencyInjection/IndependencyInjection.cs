using Domain.Repositories;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace Infrastructure.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // OPTIMIZACIÓN: AddDbContextPool en lugar de AddDbContext mejora el rendimiento en escenarios de alta concurrencia.
            services.AddDbContextPool<LLASantaFeDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptionsAction =>
                {
                    npgsqlOptionsAction.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            });

            // Registro de Repositorios basados en tus nuevas entidades
            services.AddScoped<ICiudadRepository, CiudadRepository>();
            services.AddScoped<IDepartamentoRepository, DepartamentoRepository>();
            services.AddScoped<IEncuestaRepository, EncuestaRepository>();
            services.AddScoped<IEncuestaRespuestaRepository, EncuestaRespuestaRepository>();
            services.AddScoped<IEventoRepository, EventoRepository>();
            services.AddScoped<INoticiaRepository, NoticiaRepository>();
            services.AddScoped<INotificacionRepository, NotificacionRepository>();
            services.AddScoped<IPropuestaRepository, PropuestaRepository>();
            services.AddScoped<IRepresentanteRepository, RepresentanteRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<ISedeRepository, SedeRepository>();
            services.AddScoped<ISolicitudesAfiliacionRepository, SolicitudesAfiliacionRepository>();
            services.AddScoped<ISolicitudesCiudadanaRepository, SolicitudesCiudadanaRepository>();
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();

            // Si llegás a usar Gmail o MercadoPago para aportes de campaña
            // services.AddScoped<GmailClient>();

            var retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            var circuitBreakerPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30));

            // Ejemplo por si integran pasarela de donaciones
            /* 
            services.AddHttpClient<MercadoPagoClient>()
                    .AddPolicyHandler(retryPolicy)
                    .AddPolicyHandler(circuitBreakerPolicy);
            */

            return services;
        }
    }
}