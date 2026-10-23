using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace Infrastructure.Persistence;

public sealed class DatabaseConsumptionOptions
{
    [Range(1, 256)] public int ApiMaxPoolSize { get; set; } = 20;
    [Range(1, 8)] public int MailMaxPoolSize { get; set; } = 2;
    [Range(1, 256)] public int ContextPoolSize { get; set; } = 32;
    [Range(1, 30)] public int ConnectionTimeoutSeconds { get; set; } = 5;
    [Range(1, 60)] public int CommandTimeoutSeconds { get; set; } = 15;
    [Range(30, 600)] public int ConnectionIdleSeconds { get; set; } = 60;
    // Sin idempotencia general, una escritura de resultado incierto no se repite automáticamente.
    [Range(0, 2)] public int RetryCount { get; set; } = 0;

    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), true);

    public string ConnectionString(string original, bool mail)
    {
        Validate();
        return new NpgsqlConnectionStringBuilder(original)
        {
            Pooling = true, MinPoolSize = 0,
            MaxPoolSize = mail ? MailMaxPoolSize : ApiMaxPoolSize,
            Timeout = ConnectionTimeoutSeconds, CommandTimeout = CommandTimeoutSeconds,
            ConnectionIdleLifetime = ConnectionIdleSeconds, ConnectionPruningInterval = 10,
            ApplicationName = mail ? "LLASantaFeCorreo" : "LLASantaFeApi"
        }.ConnectionString;
    }
}
