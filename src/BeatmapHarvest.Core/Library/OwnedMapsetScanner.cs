using System.Globalization;
using System.Text.RegularExpressions;

namespace BeatmapHarvest.Core.Library;

/// <summary>
/// Finds beatmapset ids already on disk from file/folder names that start with the id
/// (osu! stable names Songs folders "&lt;id&gt; Artist - Title"). osu!lazer stores hashed files and cannot be scanned this way.
/// </summary>
public static partial class OwnedMapsetScanner
{
    public static long? ParseSetId(string name)
    {
        var match = LeadingId().Match(name);
        return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;
    }

    /// <summary>
    /// Ids from <c>.osz</c> files (and, when <paramref name="includeDirectories"/>, folders) in <paramref name="directory"/>.
    /// Missing directories are ignored; unfinished <c>.part</c> downloads are not counted.
    /// </summary>
    public static HashSet<long> Scan(string? directory, bool includeDirectories)
    {
        var ids = new HashSet<long>();
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return ids;

        foreach (var file in Directory.EnumerateFiles(directory, "*.osz"))
        {
            if (ParseSetId(Path.GetFileName(file)) is { } id)
                ids.Add(id);
        }

        if (includeDirectories)
        {
            foreach (var folder in Directory.EnumerateDirectories(directory))
            {
                if (ParseSetId(Path.GetFileName(folder)) is { } id)
                    ids.Add(id);
            }
        }
        return ids;
    }

    [GeneratedRegex(@"^(\d+)(?:[ _.\-]|$)")]
    private static partial Regex LeadingId();
}
