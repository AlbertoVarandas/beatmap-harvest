namespace BeatmapHarvest.Core.Library;

/// <summary>Whether a beatmapset is already on this computer, and where.</summary>
public enum MapOwnership
{
    /// <summary>Not found anywhere.</summary>
    None,

    /// <summary>Downloaded by Beatmap Harvest (done.json or the downloads folder) but not in osu! yet.</summary>
    Downloaded,

    /// <summary>In osu!: listed in osu!.db or present in the Songs folder.</summary>
    InGame,
}

/// <summary>Which beatmapsets are already on this computer.</summary>
public sealed class OwnershipIndex(IReadOnlySet<long> inGame, IReadOnlySet<long> downloaded)
{
    public static OwnershipIndex Empty { get; } = new(new HashSet<long>(), new HashSet<long>());

    public IReadOnlySet<long> InGame { get; } = inGame;
    public IReadOnlySet<long> Downloaded { get; } = downloaded;

    public MapOwnership Of(long setId) =>
        InGame.Contains(setId) ? MapOwnership.InGame
        : Downloaded.Contains(setId) ? MapOwnership.Downloaded
        : MapOwnership.None;

    public bool Owns(long setId) => InGame.Contains(setId) || Downloaded.Contains(setId);
}

/// <summary>Combines every source of "already have it": osu!.db, osu!'s Songs folder, done.json and the downloads folder.</summary>
public static class OwnedMapsets
{
    public static OwnershipIndex Collect(DownloadLedger ledger, string? downloadsDirectory, string? songsDirectory, OsuDatabase? database = null)
    {
        var inGame = OwnedMapsetScanner.Scan(songsDirectory, includeDirectories: true);
        if (database is not null)
            inGame.UnionWith(database.BeatmapsetIds());

        var downloaded = new HashSet<long>(ledger.Snapshot());
        downloaded.UnionWith(OwnedMapsetScanner.Scan(downloadsDirectory, includeDirectories: false));
        return new OwnershipIndex(inGame, downloaded);
    }
}
