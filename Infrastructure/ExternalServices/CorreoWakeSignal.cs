using Application.Interfaces;

namespace Infrastructure.ExternalServices;

public sealed class CorreoWakeSignal : ICorreoWakeSignal, IDisposable
{
    // Agrupa notificaciones sin acumular una entrada por correo en memoria.
    private readonly SemaphoreSlim signal = new(0, 1);
    public void Notify()
    {
        try { signal.Release(); }
        catch (SemaphoreFullException) { }
    }
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
    public void Dispose() => signal.Dispose();
}
