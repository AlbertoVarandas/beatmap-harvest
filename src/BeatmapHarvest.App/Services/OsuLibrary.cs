using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// The local osu! stable installation and what is already in it: reads <c>osu!.db</c> (a copy, so osu! can keep running)
/// and combines it with the Songs folder and Beatmap Harvest's own downloads. Cached until <see cref="Reload"/>.
/// </summary>
public sealed class OsuLibrary(SettingsStore settings, DownloadLedger ledger, AppPaths paths)
{
    private readonly Lock _lock = new();
    private Task<LibrarySnapshot>? _snapshot;

    public event Action? Changed;

    public OsuInstall? Install => OsuInstall.Find(settings.Load().OsuDirectory);

    public string SongsDirectory =>
        settings.Load().SongsDirectory ?? Install?.SongsDirectory ?? AppPaths.DefaultSongsDirectory;

    public string DownloadsDirectory => settings.Load().DownloadsDirectory ?? paths.DefaultDownloadsDirectory;

    /// <summary>The installed maps and the ownership of every beatmapset; computed once in the background.</summary>
    public Task<LibrarySnapshot> GetAsync()
    {
        lock (_lock)
            return _snapshot ??= Task.Run(Load);
    }

    /// <summary>Forget the cached state (after downloads, moving files or changing folders).</summary>
    public void Reload()
    {
        lock (_lock)
            _snapshot = null;
        Changed?.Invoke();
    }

    private LibrarySnapshot Load()
    {
        var install = Install;
        OsuDatabase? database = null;
        string? error = null;
        if (install is not null && File.Exists(install.DatabaseFile))
        {
            try
            {
                database = OsuDatabaseReader.Read(install.DatabaseFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                error = $"Could not read osu!.db ({ex.Message}). Only the Songs folder is used.";
            }
        }

        OwnershipIndex ownership;
        try
        {
            ownership = OwnedMapsets.Collect(ledger, DownloadsDirectory, SongsDirectory, database);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ownership = new OwnershipIndex(database?.BeatmapsetIds() ?? [], new HashSet<long>(ledger.Snapshot()));
            error = $"Could not read the downloads or Songs folder ({ex.Message}).";
        }
        return new LibrarySnapshot(install, SongsDirectory, database, ownership, error);
    }
}

public sealed record LibrarySnapshot(OsuInstall? Install, string SongsDirectory, OsuDatabase? Database, OwnershipIndex Ownership, string? Error);
