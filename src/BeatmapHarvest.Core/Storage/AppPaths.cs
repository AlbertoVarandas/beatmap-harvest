using System.Globalization;

namespace BeatmapHarvest.Core.Storage;

/// <summary>Where the app keeps its data. Defaults to <c>%LOCALAPPDATA%\BeatmapHarvest</c>.</summary>
public sealed class AppPaths(string root)
{
    /// <summary>
    /// %LOCALAPPDATA%\BeatmapHarvest, or the folder in the <c>BEATMAPHARVEST_DATA</c> environment variable
    /// (a portable setup, or a separate profile for testing).
    /// </summary>
    public static AppPaths Default { get; } = new(
        Environment.GetEnvironmentVariable("BEATMAPHARVEST_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeatmapHarvest"));

    /// <summary>osu! stable's Songs folder in its default install location.</summary>
    public static string DefaultSongsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!", "Songs");

    public string Root { get; } = root;

    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string CredentialsFile => Path.Combine(Root, "credentials.bin");
    public string AccountFile => Path.Combine(Root, "account.bin");
    public string LedgerFile => Path.Combine(Root, "done.json");

    /// <summary>Cache of what each file in osu!lazer's store is (see LazerLibraryScanner).</summary>
    public string LazerIndexFile => Path.Combine(Root, "lazer-index.json");

    /// <summary>Folder osu!lazer imports collections from ("Import from osu!stable") when osu! stable is not installed.</summary>
    public string LazerCollectionsDirectory => Path.Combine(Root, "osu!lazer collections");
    public string DefaultDownloadsDirectory => Path.Combine(Root, "downloads");
    public string ProfilesDirectory => Path.Combine(Root, "profiles");

    public string ProfileDirectory(long userId) =>
        Path.Combine(ProfilesDirectory, userId.ToString(CultureInfo.InvariantCulture));

    public string HarvestFile(long userId) => Path.Combine(ProfileDirectory(userId), "beatmap_info.json");
}
