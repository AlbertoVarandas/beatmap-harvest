using System.Collections.Concurrent;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Full beatmapset details for the details panel, fetched once per set and kept for the session.
/// A failed request is forgotten so the next open tries again.
/// </summary>
public sealed class MapDetailsService(IOsuDataSource api)
{
    private readonly ConcurrentDictionary<long, Task<OsuBeatmapsetDetails>> _cache = new();

    public Task<OsuBeatmapsetDetails> GetAsync(long beatmapsetId)
    {
        var task = _cache.GetOrAdd(beatmapsetId, id => api.GetBeatmapsetAsync(id, CancellationToken.None));
        if (task.IsFaulted || task.IsCanceled)
        {
            _cache.TryRemove(new KeyValuePair<long, Task<OsuBeatmapsetDetails>>(beatmapsetId, task));
            task = _cache.GetOrAdd(beatmapsetId, id => api.GetBeatmapsetAsync(id, CancellationToken.None));
        }
        return task;
    }
}
