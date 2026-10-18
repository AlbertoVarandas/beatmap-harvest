using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeatmapHarvest.Core.Json;

/// <summary>Shared serializer settings. The osu! API and our own files both use snake_case.</summary>
public static class JsonDefaults
{
    /// <summary>For reading osu! API responses.</summary>
    public static JsonSerializerOptions Api { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>For files written to disk (harvest results, configs, settings).</summary>
    public static JsonSerializerOptions Files { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        // Keep artist/title text readable in the file instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };
}
