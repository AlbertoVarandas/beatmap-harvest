using System.Globalization;

namespace BeatmapHarvest.Core.Storage;

/// <summary>Where the app keeps its data. Defaults to <c>%LOCALAPPDATA%\BeatmapHarvest</c>.</summary>
public sealed class AppPaths(string root)
{
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeatmapHarvest"));

    /// <summary>osu! stable's Songs folder in its default install location.</summary>
    public static string DefaultSongsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "osu!", "Songs");

    public string Root { get; } = root;

    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string CredentialsFile => Path.Combine(Root, "credentials.bin");
    public string LedgerFile => Path.Combine(Root, "done.json");
    public string DefaultDownloadsDirectory => Path.Combine(Root, "downloads");
    public string ProfilesDirectory => Path.Combine(Root, "profiles");

    public string ProfileDirectory(long userId) =>
        Path.Combine(ProfilesDirectory, userId.ToString(CultureInfo.InvariantCulture));

    public string HarvestFile(long userId) => Path.Combine(ProfileDirectory(userId), "beatmap_info.json");
}
