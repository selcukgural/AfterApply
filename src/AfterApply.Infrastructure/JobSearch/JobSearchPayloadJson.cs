using System.Text.Json;
using System.Text.Json.Serialization;

namespace AfterApply.Infrastructure.JobSearch;

/// <summary>
/// How the Application DTOs go into and come out of the jsonb columns. The stored shape is the
/// product's own response type, so a hit is one deserialisation and no mapping. Every stored row
/// carries <see cref="SchemaVersion"/>; bump it when a DTO changes shape and older rows become
/// misses instead of being read into a type they no longer match.
/// </summary>
public static class JobSearchPayloadJson
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>Null when the JSON does not fit <typeparamref name="T"/> — the caller treats
    /// that as a cache miss, never as an error the user sees.</summary>
    public static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
