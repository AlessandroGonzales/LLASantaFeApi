using Application.Interfaces;
using Domain.Repositories;
namespace Application.Services;
public sealed class CorreoDispatcher(INotificacionRepository repository,ICorreoSender sender)
{
    public async Task<bool> ProcesarUnoAsync(int limiteDiario,CancellationToken ct)
    {
        var correo=await repository.ReservarAsync(limiteDiario,ct);
        if (correo is null) return false;
        ResultadoCorreo resultado;
        try { resultado=await sender.EnviarAsync(correo,ct); }
        catch (Exception) { resultado=new("incierto",Error:"envio_interrumpido"); }
        var estado=resultado.Estado;
        if (estado=="pendiente" && correo.Intentos>=5) estado="fallido";
        // Tras cancelación del host aún intentamos persistir el resultado, con plazo acotado.
        using var finish=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await repository.FinalizarAsync(correo.Id,estado,resultado.ProveedorId,resultado.Error,
            (int)Math.Pow(2,correo.Intentos)*60,finish.Token);
        return true;
    }
}
