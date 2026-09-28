namespace Domain.Models;
public sealed class SolicitudVista
{
    public Guid Id { get; set; }
    public string Motivo { get; set; } = "";
    public string Estado { get; set; } = "";
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool TienePdf { get; set; }
}
public sealed class SolicitudDetalle
{
    public Guid Id { get; set; }
    public string Motivo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? Respuesta { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool TienePdf { get; set; }
}
public sealed class SolicitudPdfDatos
{
    public byte[] Contenido { get; set; } = [];
}
