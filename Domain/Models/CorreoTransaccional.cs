namespace Domain.Models;
public sealed record NotificacionEstado(Guid Id,string Estado);
public sealed record CorreoPendiente(Guid Id,string Tipo,string Email,int Intentos);
