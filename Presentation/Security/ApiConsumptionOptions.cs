using System.ComponentModel.DataAnnotations;

namespace Presentation.Security;

public sealed class ApiConsumptionOptions
{
    [Range(1, 10000)] public int General { get; set; } = 120;
    [Range(1, 10000)] public int Login { get; set; } = 10;
    [Range(1, 10000)] public int Registro { get; set; } = 5;
    [Range(1, 10000)] public int Escritura { get; set; } = 20;
    [Range(1, 10000)] public int CuentaCorreo { get; set; } = 5;
    [Range(1, 256)] public int ReadConcurrency { get; set; } = 8;
    [Range(1, 256)] public int WriteConcurrency { get; set; } = 6;
    [Range(1, 8)] public int AuthenticationConcurrency { get; set; } = 2;
    [Range(1, 8)] public int PdfConcurrency { get; set; } = 2;
    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);
}

public enum ApiWorkload { Read, Write, Authentication, Pdf }

[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiWorkloadAttribute(ApiWorkload workload) : Attribute
{
    public ApiWorkload Workload { get; } = workload;
}
