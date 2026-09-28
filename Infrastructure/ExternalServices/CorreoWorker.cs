using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Infrastructure.ExternalServices;
public sealed class CorreoWorker(IServiceScopeFactory scopes,GmailOptions options,ILogger<CorreoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay=TimeSpan.FromSeconds(15);
            try
            {
                using var scope=scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<CorreoDispatcher>().ProcesarUnoAsync(options.DailyLimit,stoppingToken))
                    delay=TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // No registrar mensajes de Google, direcciones ni credenciales.
                logger.LogError("Fallo en cola de correos. Tipo={Tipo}",ex.GetType().Name);
                delay=TimeSpan.FromSeconds(30);
            }
            try { await Task.Delay(delay,stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
