using BeatmapHarvest.Core.Harvesting;

namespace BeatmapHarvest.Core.Storage;

/// <summary>Saves and loads one <c>beatmap_info.json</c> per profile.</summary>
public sealed class HarvestStore(AppPaths paths)
{
    public void Save(HarvestResult result) => AtomicFile.WriteJson(paths.HarvestFile(result.UserId), result);

    public HarvestResult? Load(long userId) => AtomicFile.ReadJsonOrDefault<HarvestResult?>(paths.HarvestFile(userId), null);

    /// <summary>Every saved harvest, newest first.</summary>
    public IReadOnlyList<HarvestResult> LoadAll()
    {
        if (!Directory.Exists(paths.ProfilesDirectory))
            return [];

        return Directory.EnumerateDirectories(paths.ProfilesDirectory)
            .Select(dir => long.TryParse(Path.GetFileName(dir), out var id) ? Load(id) : null)
            .OfType<HarvestResult>()
            .OrderByDescending(r => r.HarvestedAt)
            .ToList();
    }
}
