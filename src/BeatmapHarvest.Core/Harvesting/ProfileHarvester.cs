using System.Net;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Harvesting;

public sealed record HarvestOptions
{
    /// <summary>Score lists to read. most_played is always read.</summary>
    public IReadOnlyList<ScoreType> ScoreTypes { get; init; } = [ScoreType.Best, ScoreType.Firsts, ScoreType.Pinned, ScoreType.Recent];

    /// <summary>Rulesets whose score lists are read. most_played already covers every mode.</summary>
    public IReadOnlyList<string> ScoreModes { get; init; } = Ruleset.All;

    public int PageSize { get; init; } = 100;
}

public sealed record HarvestProgress(string Message, int MapsetsFound);

/// <summary>Collects every beatmapset linked to a profile (score lists + most played).</summary>
public sealed class ProfileHarvester(IOsuDataSource api)
{
    // Guards against a misbehaving endpoint that never returns an empty page.
    private const int MaxPages = 1000;

    public async Task<HarvestResult> HarvestAsync(
        UserReference profile,
        HarvestOptions options,
        IProgress<HarvestProgress>? progress,
        CancellationToken cancellationToken)
    {
        var aggregator = new MapsetAggregator();
        void Report(string message) => progress?.Report(new HarvestProgress(message, aggregator.Count));

        Report($"Looking up {profile.ApiKey}…");
        var user = await api.GetUserAsync(profile, cancellationToken).ConfigureAwait(false);
        Report($"Found {user.Username} (id {user.Id}).");

        foreach (var type in options.ScoreTypes)
        {
            foreach (var mode in options.ScoreModes)
            {
                var source = HarvestSource.From(type);
                try
                {
                    var count = await PaginateAsync(
                        (offset, ct) => api.GetScoresAsync(user.Id, type, mode, options.PageSize, offset, ct),
                        page => { foreach (var score in page) aggregator.AddScore(source, score); },
                        options.PageSize,
                        cancellationToken).ConfigureAwait(false);
                    Report($"Scores {source} [{mode}]: {count}.");
                }
                catch (OsuApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
                {
                    // A list that the API does not offer for this profile/mode should not abort the harvest.
                    Report($"Scores {source} [{mode}]: skipped ({ex.Message}).");
                }
            }
        }

        var played = await PaginateAsync(
            (offset, ct) => api.GetMostPlayedAsync(user.Id, options.PageSize, offset, ct),
            page =>
            {
                foreach (var entry in page) aggregator.AddMostPlayed(entry);
                Report($"Most played: {aggregator.Count} beatmapsets so far…");
            },
            options.PageSize,
            cancellationToken).ConfigureAwait(false);
        Report($"Most played: {played} difficulties.");

        var mapsets = aggregator.Build();
        Report($"Done: {mapsets.Count} beatmapsets.");

        return new HarvestResult
        {
            UserId = user.Id,
            Username = user.Username,
            DefaultMode = user.Playmode,
            HarvestedAt = DateTimeOffset.UtcNow,
            Mapsets = mapsets,
        };
    }

    /// <summary>Requests pages until an empty one comes back. Returns the number of items read.</summary>
    internal static async Task<int> PaginateAsync<T>(
        Func<int, CancellationToken, Task<IReadOnlyList<T>>> fetch,
        Action<IReadOnlyList<T>> consume,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var offset = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var items = await fetch(offset, cancellationToken).ConfigureAwait(false);
            if (items.Count == 0)
                break;
            consume(items);
            offset += items.Count;
        }
        return offset;
    }
}
