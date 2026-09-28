using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Exceptions;
namespace Application.Validation;

// Contrato v1. La configuración y las respuestas usan IDs estables, no etiquetas.
public static class EncuestaValidator
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false, MaxDepth = 16
    };
    public static string Validate(string configuration, Dictionary<string, JsonElement> answers)
    {
        var config = ReadConfiguration(configuration, "conflict");
        return ValidateAnswers(config, answers);
    }
    public static string ValidateConfiguration(JsonElement configuration)
    {
        var json = configuration.ValueKind == JsonValueKind.Object ? configuration.GetRawText() : "null";
        ReadConfiguration(json, "validation");
        return json;
    }
    private static Config ReadConfiguration(string configuration, string errorCode)
    {
        Config config;
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(configuration) > 32768) throw new JsonException();
            config = JsonSerializer.Deserialize<Config>(configuration, Options) ?? throw new JsonException();
            if (config.Version != 1 || config.Preguntas is null || config.Preguntas.Count is < 1 or > 50 ||
                config.Preguntas.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 64 ||
                    string.IsNullOrWhiteSpace(p.Texto) || p.Texto.Length > 500 ||
                    p.Tipo is not ("opcion_unica" or "opcion_multiple" or "texto" or "numero")) ||
                config.Preguntas.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != config.Preguntas.Count)
                throw new JsonException();
            foreach (var p in config.Preguntas)
            {
                if (p.MaxLongitud is < 1 or > 2000 || p.Min > p.Max) throw new JsonException();
                if (p.Tipo != "texto" && p.MaxLongitud is not null ||
                    p.Tipo != "numero" && (p.Min is not null || p.Max is not null) ||
                    p.Tipo is "texto" or "numero" && p.Opciones is not null) throw new JsonException();
                if (p.Tipo is "opcion_unica" or "opcion_multiple")
                {
                    if (p.Opciones is null || p.Opciones.Count is < 2 or > 20 ||
                        p.Opciones.Any(o => o is null || string.IsNullOrWhiteSpace(o.Id) || o.Id.Length > 64 ||
                            string.IsNullOrWhiteSpace(o.Texto) || o.Texto.Length > 300) ||
                        p.Opciones.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != p.Opciones.Count)
                        throw new JsonException();
                }
            }
        }
        catch (JsonException) { throw new BusinessException(errorCode, "La encuesta no tiene una configuración v1 válida (máximo 32 KiB y 50 preguntas)."); }
        return config;
    }
    private static string ValidateAnswers(Config config, Dictionary<string, JsonElement> answers)
    {
        if (answers.Count is < 1 or > 50 || answers.Keys.Any(id => !config.Preguntas.Any(p => p.Id == id)))
            throw InvalidAnswer();
        foreach (var p in config.Preguntas)
        {
            if (!answers.TryGetValue(p.Id, out var value))
            {
                if (p.Obligatoria) throw InvalidAnswer();
                continue;
            }
            var valid = p.Tipo switch
            {
                "texto" => value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()) && value.GetString()!.Length <= (p.MaxLongitud ?? 2000),
                "numero" => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n) &&
                    (p.Min is null || n >= p.Min) && (p.Max is null || n <= p.Max),
                "opcion_unica" => value.ValueKind == JsonValueKind.String && p.Opciones!.Any(o => o.Id == value.GetString()),
                "opcion_multiple" => MultipleIsValid(value, p.Opciones!),
                _ => false
            };
            if (!valid) throw InvalidAnswer();
        }
        return JsonSerializer.Serialize(answers);
    }
    private static bool MultipleIsValid(JsonElement value, List<Opcion> options)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 20) return false;
        var selected = new HashSet<string>(StringComparer.Ordinal);
        return value.EnumerateArray().All(v => v.ValueKind == JsonValueKind.String &&
            options.Any(o => o.Id == v.GetString()) && selected.Add(v.GetString()!));
    }
    private static BusinessException InvalidAnswer() => new("validation", "Las respuestas no corresponden a las preguntas, opciones o límites de la encuesta.");
    private sealed class Config
    {
        public int Version { get; set; }
        public List<Pregunta> Preguntas { get; set; } = [];
    }
    private sealed class Pregunta
    {
        public string Id { get; set; } = "";
        public string Texto { get; set; } = "";
        public string Tipo { get; set; } = "";
        public bool Obligatoria { get; set; }
        public List<Opcion>? Opciones { get; set; }
        public int? MaxLongitud { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
    }
    private sealed class Opcion
    {
        public string Id { get; set; } = "";
        public string Texto { get; set; } = "";
    }
}
