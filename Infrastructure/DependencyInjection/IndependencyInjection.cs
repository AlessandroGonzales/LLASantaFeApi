using Domain.Repositories;
using Application.Authentication;
using Infrastructure.Authentication;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection outside source control.");

            // OPTIMIZACIÓN: AddDbContextPool en lugar de AddDbContext mejora el rendimiento en escenarios de alta concurrencia.
            services.AddDbContextPool<LLASantaFeDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptionsAction =>
                {
                    npgsqlOptionsAction.SetPostgresVersion(16, 0);
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
            services.AddSingleton<Npgsql.NpgsqlDataSource>(_ => Npgsql.NpgsqlDataSource.Create(
                new Npgsql.NpgsqlConnectionStringBuilder(connectionString) {MaxPoolSize=5}.ConnectionString));
            var gmail=new Infrastructure.ExternalServices.GmailOptions
            {
                Enabled=bool.Parse(configuration["Gmail:Enabled"] ?? "false"),
                Sender=configuration["Gmail:Sender"] ?? "",
                ClientId=configuration["Gmail:ClientId"] ?? "",
                ClientSecret=configuration["Gmail:ClientSecret"] ?? "",
                RefreshToken=configuration["Gmail:RefreshToken"] ?? "",
                DailyLimit=int.Parse(configuration["Gmail:DailyLimit"] ?? "100")
            };
            gmail.Validate();
            services.AddSingleton(gmail);
            if (gmail.Enabled)
            {
                services.AddSingleton<Application.Interfaces.ICorreoSender,Infrastructure.ExternalServices.GmailCorreoSender>();
                services.AddScoped<Application.Services.CorreoDispatcher>();
                services.AddHostedService<Infrastructure.ExternalServices.CorreoWorker>();
            }
            services.AddScoped<IPropuestaRepository, PropuestaRepository>();
            services.AddScoped<IRepresentanteRepository, RepresentanteRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<ISedeRepository, SedeRepository>();
            services.AddScoped<ISolicitudesAfiliacionRepository, SolicitudesAfiliacionRepository>();
            services.AddScoped<ISolicitudesCiudadanaRepository, SolicitudesCiudadanaRepository>();
            services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            services.AddScoped<IParticipacionUsuarioRepository, ParticipacionUsuarioRepository>();
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<Application.Interfaces.IPdfScanner, Infrastructure.ExternalServices.ClamAvPdfScanner>();
            services.AddScoped<ITokenService, JwtService>();

            return services;
        }
    }
}
