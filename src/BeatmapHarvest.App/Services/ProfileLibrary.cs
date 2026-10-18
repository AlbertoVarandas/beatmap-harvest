using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>A harvested profile as listed in the sidebar.</summary>
public sealed record ProfileEntry(long UserId, string Username, DateTimeOffset HarvestedAt, int Mapsets);

/// <summary>
/// Every profile harvested so far (one <c>beatmap_info.json</c> each), loaded once and kept in memory.
/// The sidebar lists them; the Beatmaps page shows one at a time.
/// </summary>
public sealed class ProfileLibrary(HarvestStore store)
{
    private readonly Lock _lock = new();
    private Dictionary<long, HarvestResult>? _results;

    public event Action? Changed;

    /// <summary>Newest harvest first.</summary>
    public IReadOnlyList<ProfileEntry> Profiles
    {
        get
        {
            lock (_lock)
            {
                return Results().Values
                    .OrderByDescending(r => r.HarvestedAt)
                    .Select(r => new ProfileEntry(r.UserId, r.Username, r.HarvestedAt, r.Mapsets.Count))
                    .ToList();
            }
        }
    }

    public HarvestResult? Get(long userId)
    {
        lock (_lock)
            return Results().GetValueOrDefault(userId);
    }

    public HarvestResult? Latest
    {
        get
        {
            lock (_lock)
                return Results().Values.MaxBy(r => r.HarvestedAt);
        }
    }

    /// <summary>Saves a new harvest and makes it the newest profile.</summary>
    public void Put(HarvestResult result)
    {
        store.Save(result);
        lock (_lock)
            Results()[result.UserId] = result;
        Changed?.Invoke();
    }

    public void Remove(long userId)
    {
        store.Delete(userId);
        lock (_lock)
            Results().Remove(userId);
        Changed?.Invoke();
    }

    private Dictionary<long, HarvestResult> Results() =>
        _results ??= store.LoadAll().DistinctBy(r => r.UserId).ToDictionary(r => r.UserId);
}
