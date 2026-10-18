namespace BeatmapHarvest.Core.Library;

/// <summary>
/// An osu! stable installation: where its Songs folder, databases and skins are.
/// The Songs folder and current skin come from the user's <c>osu!.&lt;user&gt;.cfg</c> when present.
/// osu!lazer keeps files by hash in a Realm database and is not supported.
/// </summary>
public sealed record OsuInstall(string Root, string SongsDirectory, string? CurrentSkin)
{
    public string DatabaseFile => Path.Combine(Root, "osu!.db");
    public string CollectionFile => Path.Combine(Root, "collection.db");
    public string SkinsDirectory => Path.Combine(Root, "Skins");

    public static string DefaultRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!");

    /// <summary>The configured folder if given, otherwise the usual install locations. Null when osu! stable is not found.</summary>
    public static OsuInstall? Find(string? configuredRoot)
    {
        IEnumerable<string> candidates = configuredRoot is not null
            ? [configuredRoot]
            :
            [
                DefaultRoot,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "osu!"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "osu!"),
            ];

        foreach (var root in candidates)
        {
            if (File.Exists(Path.Combine(root, "osu!.exe")) || File.Exists(Path.Combine(root, "osu!.db")))
                return FromRoot(root);
        }
        return null;
    }

    internal static OsuInstall FromRoot(string root)
    {
        var config = ReadUserConfig(root);
        var songs = config.GetValueOrDefault("BeatmapDirectory") is { Length: > 0 } dir
            ? Path.GetFullPath(Path.IsPathRooted(dir) ? dir : Path.Combine(root, dir))
            : Path.Combine(root, "Songs");
        return new OsuInstall(root, songs, config.GetValueOrDefault("Skin") is { Length: > 0 } skin ? skin : null);
    }

    /// <summary>Settings from <c>osu!.&lt;windows user&gt;.cfg</c> (or the only per-user cfg there is).</summary>
    internal static Dictionary<string, string> ReadUserConfig(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root))
            return result;

        var own = Path.Combine(root, $"osu!.{Environment.UserName}.cfg");
        var file = File.Exists(own)
            ? own
            : Directory.EnumerateFiles(root, "osu!.*.cfg").FirstOrDefault(f => !Path.GetFileName(f).Equals("osu!.cfg", StringComparison.OrdinalIgnoreCase));
        if (file is null)
            return result;

        foreach (var line in File.ReadLines(file))
        {
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0 || line.TrimStart().StartsWith('#'))
                continue;
            result[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return result;
    }
}
