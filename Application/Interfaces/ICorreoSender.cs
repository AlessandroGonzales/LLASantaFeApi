using Domain.Models;
namespace Application.Interfaces;
public sealed record ResultadoCorreo(string Estado,string? ProveedorId=null,string? Error=null);
public interface ICorreoSender
{
    Task<ResultadoCorreo> EnviarAsync(CorreoPendiente correo,CancellationToken ct);
}
