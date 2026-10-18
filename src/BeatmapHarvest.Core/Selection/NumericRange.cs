using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeatmapHarvest.Core.Selection;

/// <summary>An inclusive range; null bounds are open. Serialised as <c>[min, max]</c> with null for "no limit".</summary>
[JsonConverter(typeof(NumericRangeJsonConverter))]
public readonly record struct NumericRange(double? Min, double? Max)
{
    public static NumericRange Any => default;

    public bool Contains(double value) => (Min is not { } min || value >= min) && (Max is not { } max || value <= max);
}

internal sealed class NumericRangeJsonConverter : JsonConverter<NumericRange>
{
    public override NumericRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return NumericRange.Any;
        var values = JsonSerializer.Deserialize<double?[]>(ref reader, options) ?? [];
        return new NumericRange(values.ElementAtOrDefault(0), values.ElementAtOrDefault(1));
    }

    public override void Write(Utf8JsonWriter writer, NumericRange value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        WriteBound(writer, value.Min);
        WriteBound(writer, value.Max);
        writer.WriteEndArray();
    }

    private static void WriteBound(Utf8JsonWriter writer, double? bound)
    {
        if (bound is { } b)
            writer.WriteNumberValue(b);
        else
            writer.WriteNullValue();
    }
}
