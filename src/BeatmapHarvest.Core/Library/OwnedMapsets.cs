namespace BeatmapHarvest.Core.Library;

/// <summary>Combines every source of "already have it": done.json, the downloads folder and osu!'s Songs folder.</summary>
public static class OwnedMapsets
{
    public static HashSet<long> Collect(DownloadLedger ledger, string? downloadsDirectory, string? songsDirectory)
    {
        var owned = new HashSet<long>(ledger.Snapshot());
        owned.UnionWith(OwnedMapsetScanner.Scan(downloadsDirectory, includeDirectories: false));
        owned.UnionWith(OwnedMapsetScanner.Scan(songsDirectory, includeDirectories: true));
        return owned;
    }
}
