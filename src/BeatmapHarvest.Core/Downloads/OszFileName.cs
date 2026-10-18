using System.Globalization;
using System.Text;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Downloads;

/// <summary>Builds a safe <c>.osz</c> file name that always starts with the beatmapset id.</summary>
public static class OszFileName
{
    private const int MaxLength = 150;

    public static string Build(long setId, string? suggested)
    {
        var id = setId.ToString(CultureInfo.InvariantCulture);
        var name = Sanitize(suggested);
        if (name.EndsWith(".osz", StringComparison.OrdinalIgnoreCase))
            name = name[..^4].TrimEnd();
        if (name.Length == 0)
            return id + ".osz";

        if (OwnedMapsetScanner.ParseSetId(name) != setId)
            name = $"{id} {name}";
        if (name.Length > MaxLength)
            name = name[..MaxLength].TrimEnd();
        return name + ".osz";
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim().Trim('"'))
            builder.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
        // Windows does not allow names ending with a dot or space.
        return builder.ToString().Trim().TrimEnd('.');
    }
}
