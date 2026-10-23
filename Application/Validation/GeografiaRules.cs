using Domain.Exceptions;

namespace Application.Validation;

public static class GeografiaRules
{
    public static string Nombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 100 || nombre.Any(char.IsControl))
            throw new BusinessException("validation", "El nombre debe tener entre 1 y 100 caracteres, sin caracteres de control.");
        return nombre.Trim();
    }

    public static void Departamento(Guid id)
    {
        if (id == Guid.Empty)
            throw new BusinessException("validation", "Indicá un departamento válido.");
    }
}
