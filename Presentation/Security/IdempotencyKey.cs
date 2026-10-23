using Domain.Exceptions;

namespace Presentation.Security;

public static class IdempotencyKey
{
    public const string Header = "Idempotency-Key";
    public static Guid? Parse(string? value, int count)
    {
        if (count == 0) return null; // Compatibilidad con los clientes anteriores.
        if (count != 1 || !Guid.TryParse(value, out var key) || key == Guid.Empty)
            throw new BusinessException("validation", "Idempotency-Key debe contener un único UUID válido, distinto de cero.");
        return key;
    }
}
