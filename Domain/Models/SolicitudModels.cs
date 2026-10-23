namespace Domain.Models;
public sealed class SolicitudCreacion
{
    public Guid Id { get; set; }
    public bool Nueva { get; set; }
}
public sealed class SolicitudPdfReserva
{
    public Guid Intento { get; set; }
    public string? Sha256 { get; set; }
    public int? Bytes { get; set; }
    public bool Confirmada => Intento == Guid.Empty;
}
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
    public string EstadoPdf => TienePdf ? "disponible" : "sin_adjunto";
    public int? PdfBytes { get; set; }
    public string? PdfSha256 { get; set; }
}
public sealed class SolicitudPdfDatos
{
    public byte[] Contenido { get; set; } = [];
}
