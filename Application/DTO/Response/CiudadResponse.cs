namespace Application.DTO.Response
{
    public class CiudadResponse
    {
        public Guid IdCiudad { get; set; }
        public string? Nombre { get; set; }
        public Guid? DepartamentoId { get; set; }
    }
}
