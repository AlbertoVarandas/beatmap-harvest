using BeatmapHarvest.Core.Downloads;

namespace BeatmapHarvest.Core.Storage;

/// <summary>User preferences, stored in <c>settings.json</c>. Credentials are stored separately and encrypted.</summary>
public sealed record AppSettings
{
    public string? LastProfile { get; init; }

    /// <summary>Interface language (the app's own enum, stored by name); null → the Windows display language.</summary>
    public string? LanguageName { get; init; }

    /// <summary>null → <see cref="AppPaths.DefaultDownloadsDirectory"/>.</summary>
    public string? DownloadsDirectory { get; init; }

    /// <summary>Which osu! to work with; null → osu!lazer when installed, otherwise osu! stable.</summary>
    public Library.OsuClient? Client { get; init; }

    /// <summary>The osu! stable install; null → detected automatically.</summary>
    public string? OsuDirectory { get; init; }

    /// <summary>osu!lazer's data folder; null → detected automatically.</summary>
    public string? LazerDirectory { get; init; }

    /// <summary>null → the Songs folder of the osu! install (from its config), or <see cref="AppPaths.DefaultSongsDirectory"/>.</summary>
    public string? SongsDirectory { get; init; }

    /// <summary>Where "Move downloads" puts the .osz files; null → the Songs folder.</summary>
    public string? MoveToDirectory { get; init; }

    public int MaxParallelDownloads { get; init; } = 2;

    public IReadOnlyList<MirrorDefinition> Mirrors { get; init; } = MirrorDefinition.Defaults;
}

public sealed class SettingsStore(AppPaths paths)
{
    public AppSettings Load() => AtomicFile.ReadJsonOrDefault(paths.SettingsFile, new AppSettings());

    public void Save(AppSettings settings) => AtomicFile.WriteJson(paths.SettingsFile, settings);
}
