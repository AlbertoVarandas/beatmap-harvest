using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// The osu! installation the app works with (osu!lazer or osu! stable) and what is already in it.
/// For stable it reads <c>osu!.db</c> (a copy) and the Songs folder; for lazer it reads the .osu files in its store.
/// Combined with Beatmap Harvest's own downloads. Cached until <see cref="Reload"/>.
/// </summary>
public sealed class OsuLibrary(SettingsStore settings, DownloadLedger ledger, AppPaths paths, Localizer l)
{
    private readonly Lock _lock = new();
    private Task<LibrarySnapshot>? _snapshot;

    public event Action? Changed;

    public OsuClients Clients
    {
        get
        {
            var current = settings.Load();
            return OsuClients.Detect(current.OsuDirectory, current.LazerDirectory);
        }
    }

    /// <summary>The client in use: the one chosen in Settings, otherwise osu!lazer when installed.</summary>
    public OsuClient? Client => Clients.Resolve(settings.Load().Client);

    /// <summary>The osu! stable install, when it is the client in use.</summary>
    public OsuInstall? Install => Client == OsuClient.Stable ? Clients.Stable : null;

    public string SongsDirectory =>
        settings.Load().SongsDirectory ?? Clients.Stable?.SongsDirectory ?? AppPaths.DefaultSongsDirectory;

    public string DownloadsDirectory => settings.Load().DownloadsDirectory ?? paths.DefaultDownloadsDirectory;

    /// <summary>The installed maps and the ownership of every beatmapset; computed once in the background.</summary>
    public Task<LibrarySnapshot> GetAsync()
    {
        lock (_lock)
            return _snapshot ??= Task.Run(Load);
    }

    /// <summary>Forget the cached state (after downloads, moving files or changing settings).</summary>
    public void Reload()
    {
        lock (_lock)
            _snapshot = null;
        Changed?.Invoke();
    }

    private LibrarySnapshot Load()
    {
        var clients = Clients;
        var client = clients.Resolve(settings.Load().Client);
        var downloads = DownloadsDirectory;
        IReadOnlyList<LocalBeatmap> maps = [];
        OsuDatabase? database = null;
        string? error = null;
        var inGame = new HashSet<long>();

        try
        {
            if (client == OsuClient.Lazer)
            {
                maps = LazerLibraryScanner.Scan(clients.Lazer!.FilesDirectory, paths.LazerIndexFile);
            }
            else if (client == OsuClient.Stable)
            {
                var install = clients.Stable!;
                if (File.Exists(install.DatabaseFile))
                {
                    try
                    {
                        database = OsuDatabaseReader.Read(install.DatabaseFile);
                        maps = database.Beatmaps;
                    }
                    catch (InvalidDataException ex)
                    {
                        error = l.F("Could not read osu!.db ({0}). Only the Songs folder is used.", ex.Message);
                    }
                }
                inGame.UnionWith(OwnedMapsetScanner.Scan(SongsDirectory, includeDirectories: true));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = l.F("Could not read the osu! library ({0}).", ex.Message);
        }

        inGame.UnionWith(maps.Where(m => m.BeatmapsetId > 0).Select(m => m.BeatmapsetId));
        var downloaded = new HashSet<long>(ledger.Snapshot());
        try
        {
            downloaded.UnionWith(OwnedMapsetScanner.Scan(downloads, includeDirectories: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error ??= l.F("Could not read the downloads folder ({0}).", ex.Message);
        }

        return new LibrarySnapshot(clients, client, SongsDirectory, maps, database, new OwnershipIndex(inGame, downloaded), error);
    }
}

/// <param name="Clients">Every osu! found on this PC.</param>
/// <param name="Client">The one in use (null when none is installed).</param>
/// <param name="Maps">Installed difficulties of the client in use.</param>
/// <param name="Database">osu!.db, only with osu! stable (collections need its hashes).</param>
public sealed record LibrarySnapshot(
    OsuClients Clients,
    OsuClient? Client,
    string SongsDirectory,
    IReadOnlyList<LocalBeatmap> Maps,
    OsuDatabase? Database,
    OwnershipIndex Ownership,
    string? Error)
{
    public OsuInstall? Install => Client == OsuClient.Stable ? Clients.Stable : null;
}
