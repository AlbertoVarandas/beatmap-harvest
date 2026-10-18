using BeatmapHarvest.Core.Downloads;

namespace BeatmapHarvest.Core.Storage;

/// <summary>User preferences, stored in <c>settings.json</c>. Credentials are stored separately and encrypted.</summary>
public sealed record AppSettings
{
    public string? LastProfile { get; init; }

    /// <summary>null → <see cref="AppPaths.DefaultDownloadsDirectory"/>.</summary>
    public string? DownloadsDirectory { get; init; }

    /// <summary>null → <see cref="AppPaths.DefaultSongsDirectory"/>.</summary>
    public string? SongsDirectory { get; init; }

    public int MaxParallelDownloads { get; init; } = 2;

    public IReadOnlyList<MirrorDefinition> Mirrors { get; init; } = MirrorDefinition.Defaults;
}

public sealed class SettingsStore(AppPaths paths)
{
    public AppSettings Load() => AtomicFile.ReadJsonOrDefault(paths.SettingsFile, new AppSettings());

    public void Save(AppSettings settings) => AtomicFile.WriteJson(paths.SettingsFile, settings);
}
