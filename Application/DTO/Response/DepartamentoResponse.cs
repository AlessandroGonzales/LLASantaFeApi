namespace Application.DTO.Response;

public sealed class DepartamentoResponse
{
    public Guid IdDepartamento { get; init; }
    public string Nombre { get; init; } = null!;
}
