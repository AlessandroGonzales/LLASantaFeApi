namespace Application.Interfaces
{
    public interface IDepartamentoAppService
    {
        Task AgregarDepartamentoAsync (string nombre, CancellationToken cancellationToken);
    }
}
