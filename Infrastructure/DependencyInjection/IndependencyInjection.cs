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

            var database = new DatabaseConsumptionOptions();
            database.ApiMaxPoolSize=Integer("DatabaseConsumption:ApiMaxPoolSize",database.ApiMaxPoolSize);
            database.MailMaxPoolSize=Integer("DatabaseConsumption:MailMaxPoolSize",database.MailMaxPoolSize);
            database.ContextPoolSize=Integer("DatabaseConsumption:ContextPoolSize",database.ContextPoolSize);
            database.ConnectionTimeoutSeconds=Integer("DatabaseConsumption:ConnectionTimeoutSeconds",database.ConnectionTimeoutSeconds);
            database.CommandTimeoutSeconds=Integer("DatabaseConsumption:CommandTimeoutSeconds",database.CommandTimeoutSeconds);
            database.ConnectionIdleSeconds=Integer("DatabaseConsumption:ConnectionIdleSeconds",database.ConnectionIdleSeconds);
            database.RetryCount=Integer("DatabaseConsumption:RetryCount",database.RetryCount);
            database.Validate();
            var apiConnection = database.ConnectionString(connectionString, mail: false);
            services.AddSingleton(database);
            services.AddSingleton<Application.Interfaces.ICorreoWakeSignal, Infrastructure.ExternalServices.CorreoWakeSignal>();
            services.AddSingleton<ICodigoCuentaService,CodigoCuentaService>();
            var cuenta=new CuentaSecuritySettings {FrontendUrl=configuration["FrontendUrl"] ?? "http://localhost:5173"};
            cuenta.VerificacionMinutes=Integer("CuentaSecurity:VerificacionMinutes",cuenta.VerificacionMinutes);
            cuenta.RecuperacionMinutes=Integer("CuentaSecurity:RecuperacionMinutes",cuenta.RecuperacionMinutes);
            cuenta.Validate();
            services.AddSingleton(cuenta);
            // El pool de contextos reutiliza objetos; MaxPoolSize acota conexiones físicas.
            services.AddDbContextPool<LLASantaFeDbContext>(options =>
            {
                options.UseNpgsql(apiConnection, npgsqlOptionsAction =>
                {
                    npgsqlOptionsAction.SetPostgresVersion(16, 0);
                    if (database.RetryCount>0)
                        npgsqlOptionsAction.EnableRetryOnFailure(database.RetryCount,TimeSpan.FromSeconds(2),null);
                });
            }, database.ContextPoolSize);

            // Registro de Repositorios basados en tus nuevas entidades
            services.AddScoped<ICiudadRepository, CiudadRepository>();
            services.AddScoped<IDepartamentoRepository, DepartamentoRepository>();
            services.AddScoped<IEncuestaRepository, EncuestaRepository>();
            services.AddScoped<IEventoRepository, EventoRepository>();
            services.AddScoped<INoticiaRepository, NoticiaRepository>();
            services.AddScoped<INotificacionRepository, NotificacionRepository>();
            services.AddSingleton<Npgsql.NpgsqlDataSource>(_ => Npgsql.NpgsqlDataSource.Create(
                database.ConnectionString(connectionString, mail: true)));
            var gmail=new Infrastructure.ExternalServices.GmailOptions
            {
                Enabled=bool.Parse(configuration["Gmail:Enabled"] ?? "false"),
                Sender=configuration["Gmail:Sender"] ?? "",
                ClientId=configuration["Gmail:ClientId"] ?? "",
                ClientSecret=configuration["Gmail:ClientSecret"] ?? "",
                RefreshToken=configuration["Gmail:RefreshToken"] ?? ""
            };
            gmail.DailyLimit=Integer("Gmail:DailyLimit",gmail.DailyLimit);
            gmail.IdleMinSeconds=Integer("Gmail:IdleMinSeconds",gmail.IdleMinSeconds);
            gmail.IdleMaxSeconds=Integer("Gmail:IdleMaxSeconds",gmail.IdleMaxSeconds);
            gmail.Validate();
            services.AddSingleton(gmail);
            if (gmail.Enabled)
            {
                services.AddSingleton<Application.Interfaces.ICorreoSender>(provider=>new Infrastructure.ExternalServices.GmailCorreoSender(
                    gmail,provider.GetRequiredService<ICodigoCuentaService>(),cuenta));
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

            int Integer(string key,int fallback)
            {
                var value=configuration[key];
                if (value is null) return fallback;
                if (int.TryParse(value,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var number)) return number;
                throw new InvalidOperationException($"Configuración entera inválida: {key}.");
            }
        }
    }
}
