namespace BeatmapHarvest.Core.Library;

/// <summary>An installed osu! skin and the preview images it provides (paths relative to the Skins folder).</summary>
public sealed record SkinInfo(string Folder, string Name, string? Author, string? Version, IReadOnlyList<SkinElement> Previews, int Files);

public sealed record SkinElement(string Label, string RelativePath);

/// <summary>Lists the skins in osu! stable's Skins folder, reading <c>skin.ini</c> for the name and author.</summary>
public static class SkinCatalog
{
    // Shown in this order when the skin has them; @2x versions are preferred.
    private static readonly (string Label, string[] Names)[] PreviewElements =
    [
        ("Cursor", ["cursor"]),
        ("Hit circle", ["hitcircle"]),
        ("Overlay", ["hitcircleoverlay"]),
        ("Approach", ["approachcircle"]),
        ("Number", ["default-1"]),
        ("Rank S", ["ranking-S"]),
        ("Mania note", ["mania-note1"]),
        ("Taiko", ["taikohitcircle"]),
        ("Catch", ["fruit-apple"]),
        ("Menu", ["menu-background"]),
    ];

    public static IReadOnlyList<SkinInfo> List(string skinsDirectory)
    {
        if (!Directory.Exists(skinsDirectory))
            return [];
        return Directory.EnumerateDirectories(skinsDirectory)
            .Select(dir => Read(skinsDirectory, dir))
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    internal static SkinInfo Read(string skinsDirectory, string directory)
    {
        var folder = Path.GetFileName(directory);
        var ini = ReadGeneralSection(Path.Combine(directory, "skin.ini"));
        var files = Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previews = Previews(files, file => Path.GetRelativePath(skinsDirectory, Path.Combine(directory, file)));

        return new SkinInfo(folder, ini.GetValueOrDefault("Name") is { Length: > 0 } name ? name : folder,
            ini.GetValueOrDefault("Author"), ini.GetValueOrDefault("Version"), previews, files.Count);
    }

    /// <summary>Preview images of an osu!lazer skin, as paths in lazer's file store (relative to its files folder).</summary>
    public static IReadOnlyList<SkinElement> LazerPreviews(LazerSkin skin)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in skin.Files)
            byName.TryAdd(file.Filename, file.Hash);
        return Previews(byName.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), name => LazerRealmReader.StorePath(byName[name]));
    }

    /// <param name="files">Names of the skin's top-level files.</param>
    /// <param name="pathOf">Where the UI finds a file, given its name.</param>
    private static List<SkinElement> Previews(HashSet<string> files, Func<string, string> pathOf)
    {
        var previews = new List<SkinElement>();
        foreach (var (label, names) in PreviewElements)
        {
            foreach (var element in names)
            {
                var file = new[] { $"{element}@2x.png", $"{element}.png", $"{element}@2x.jpg", $"{element}.jpg" }.FirstOrDefault(files.Contains);
                if (file is not null)
                {
                    previews.Add(new SkinElement(label, pathOf(file)));
                    break;
                }
            }
        }
        return previews;
    }

    /// <summary>Key/value pairs of the [General] section of skin.ini.</summary>
    internal static Dictionary<string, string> ReadGeneralSection(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return values;

        var inGeneral = false;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inGeneral = line.Equals("[General]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (!inGeneral || colon <= 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            values.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim());
        }
        return values;
    }
}
