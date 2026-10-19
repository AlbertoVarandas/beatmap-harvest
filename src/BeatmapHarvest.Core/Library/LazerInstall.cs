namespace BeatmapHarvest.Core.Library;

/// <summary>
/// An osu!lazer installation. Its data folder (default <c>%APPDATA%\osu</c>, movable through <c>storage.ini</c>)
/// holds <c>client.realm</c> and a <c>files</c> store where every file is named by the hash of its content.
/// </summary>
public sealed record LazerInstall(string DataDirectory, string? Executable)
{
    public string FilesDirectory => Path.Combine(DataDirectory, "files");

    public string RealmFile => Path.Combine(DataDirectory, "client.realm");

    /// <summary>ID of the skin in use (<c>Skin = …</c> in game.ini); null when unknown.</summary>
    public Guid? CurrentSkin
    {
        get
        {
            var ini = Path.Combine(DataDirectory, "game.ini");
            if (!File.Exists(ini))
                return null;
            foreach (var line in File.ReadLines(ini))
            {
                var eq = line.IndexOf('=', StringComparison.Ordinal);
                if (eq > 0 && line[..eq].Trim().Equals("Skin", StringComparison.OrdinalIgnoreCase))
                    return Guid.TryParse(line[(eq + 1)..].Trim(), out var id) ? id : null;
            }
            return null;
        }
    }

    public static string DefaultDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "osu");

    public static string DefaultExecutable { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osulazer", "current", "osu!.exe");

    /// <summary>The configured data folder if given, otherwise the default one (following <c>storage.ini</c>). Null when not found.</summary>
    public static LazerInstall? Find(string? configuredDataDirectory)
    {
        var directory = configuredDataDirectory ?? FollowStorageIni(DefaultDataDirectory);
        if (!File.Exists(Path.Combine(directory, "client.realm")))
            return null;
        return new LazerInstall(directory, File.Exists(DefaultExecutable) ? DefaultExecutable : null);
    }

    /// <summary>osu!lazer can move its data elsewhere; the default folder then keeps a <c>storage.ini</c> with <c>FullPath = …</c>.</summary>
    internal static string FollowStorageIni(string directory)
    {
        var ini = Path.Combine(directory, "storage.ini");
        if (!File.Exists(ini))
            return directory;
        foreach (var line in File.ReadLines(ini))
        {
            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0 && line[..eq].Trim().Equals("FullPath", StringComparison.OrdinalIgnoreCase))
            {
                var path = line[(eq + 1)..].Trim();
                if (path.Length > 0 && Directory.Exists(path))
                    return path;
            }
        }
        return directory;
    }
}

public enum OsuClient
{
    Stable,
    Lazer,
}

/// <summary>Which osu! clients are installed, and which one the app works with.</summary>
public sealed record OsuClients(OsuInstall? Stable, LazerInstall? Lazer)
{
    public bool BothFound => Stable is not null && Lazer is not null;

    /// <summary>The preferred client when installed; otherwise osu!lazer (the current client) before osu! stable.</summary>
    public OsuClient? Resolve(OsuClient? preferred) => preferred switch
    {
        OsuClient.Stable when Stable is not null => OsuClient.Stable,
        OsuClient.Lazer when Lazer is not null => OsuClient.Lazer,
        _ when Lazer is not null => OsuClient.Lazer,
        _ when Stable is not null => OsuClient.Stable,
        _ => null,
    };

    public static OsuClients Detect(string? stableDirectory, string? lazerDataDirectory) =>
        new(OsuInstall.Find(stableDirectory), LazerInstall.Find(lazerDataDirectory));
}
