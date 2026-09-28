namespace Domain.Models;
public sealed record AfiliacionPendiente(Guid Id, string Nombre, string Apellido, DateTime FechaSolicitud, bool UsuarioActivo);
public sealed record AfiliacionContacto(Guid Id, string Estado, DateTime FechaSolicitud, string? Observacion,
    Guid UsuarioId, string Nombre, string Apellido, string Email, string? Telefono, bool UsuarioActivo,
    DateTime? AprobadaAt, Guid? AprobadaPor);
public sealed record AfiliacionPropia(Guid Id, string Estado, DateTime FechaSolicitud, DateTime? AprobadaAt);
public sealed record AfiliacionTotales(long TotalAfiliados, long TotalSolicitudes, long Pendientes, long EnRevision, long Rechazadas);
