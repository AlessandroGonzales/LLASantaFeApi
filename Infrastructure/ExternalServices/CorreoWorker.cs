using Application.Services;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Infrastructure.ExternalServices;
public sealed class CorreoWorker(IServiceScopeFactory scopes,GmailOptions options,ILogger<CorreoWorker> logger,
    ICorreoWakeSignal wake, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var emptySeconds=options.IdleMinSeconds;
        var errorSeconds=options.IdleMinSeconds;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processed;
                // Cerrar el scope antes de esperar, sin retener repositorios o conexiones.
                using (var scope=scopes.CreateScope())
                    processed=await scope.ServiceProvider.GetRequiredService<CorreoDispatcher>()
                        .ProcesarUnoAsync(options.DailyLimit,stoppingToken);
                errorSeconds=options.IdleMinSeconds;
                // Ni las señales ni una ráfaga de registros pueden generar un bucle sin espera.
                await Task.Delay(TimeSpan.FromSeconds(2),clock,stoppingToken);
                if (processed) { emptySeconds=options.IdleMinSeconds; continue; }
                var notified=await wake.WaitAsync(TimeSpan.FromSeconds(emptySeconds),stoppingToken);
                emptySeconds=notified ? options.IdleMinSeconds : Math.Min(options.IdleMaxSeconds,emptySeconds*2);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // No registrar mensajes de Google, direcciones ni credenciales.
                logger.LogError("Fallo en cola de correos. Tipo={Tipo}",ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(errorSeconds),clock,stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                errorSeconds=Math.Min(options.IdleMaxSeconds,errorSeconds*2);
            }
        }
    }
}
