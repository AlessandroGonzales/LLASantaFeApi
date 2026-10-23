namespace Application.Interfaces;

// Una señal acelera la entrega local; PostgreSQL conserva los trabajos durables.
public interface ICorreoWakeSignal
{
    void Notify();
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct);
}
