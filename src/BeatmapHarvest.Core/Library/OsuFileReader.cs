namespace BeatmapHarvest.Core.Library;

/// <summary>Reads the few things the app needs from a <c>.osu</c> difficulty file (plain text, INI-like sections).</summary>
public static class OsuFileReader
{
    /// <summary>
    /// The background image named in <c>[Events]</c> (a line like <c>0,0,"bg.jpg",0,0</c>), or null.
    /// Missing or unreadable files return null.
    /// </summary>
    public static string? ReadBackground(string osuFilePath)
    {
        try
        {
            return File.Exists(osuFilePath) ? ReadBackground(File.ReadLines(osuFilePath)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? ReadBackground(IEnumerable<string> lines)
    {
        var inEvents = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                if (inEvents)
                    return null;
                inEvents = line.Equals("[Events]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inEvents || !line.StartsWith("0,", StringComparison.Ordinal))
                continue;

            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var name = parts[2].Trim().Trim('"');
                return name.Length > 0 ? name : null;
            }
        }
        return null;
    }
}
