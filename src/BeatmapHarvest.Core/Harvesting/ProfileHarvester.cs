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

/// <param name="Fraction">Estimated progress from 0 to 1, based on the number of API requests still expected.</param>
public sealed record HarvestProgress(string Message, int MapsetsFound, double Fraction);

/// <summary>Collects every beatmapset linked to a profile (score lists + most played).</summary>
public sealed class ProfileHarvester(IOsuDataSource api)
{
    // Guards against a misbehaving endpoint that never returns an empty page.
    private const int MaxPages = 1000;

    // The API never returns more than this many best scores.
    private const int BestScoresCap = 200;

    public async Task<HarvestResult> HarvestAsync(
        UserReference profile,
        HarvestOptions options,
        IProgress<HarvestProgress>? progress,
        CancellationToken cancellationToken)
    {
        var aggregator = new MapsetAggregator();
        var budget = new RequestBudget();
        void Report(string message) => progress?.Report(new HarvestProgress(message, aggregator.Count, budget.Fraction));

        budget.Plan(1);
        Report($"Looking up {profile.ApiKey}…");
        var user = await api.GetUserAsync(profile, cancellationToken).ConfigureAwait(false);
        budget.Done();

        // The user object carries list sizes for one ruleset only; other rulesets start with a guess of one page.
        var countsMode = profile.Mode ?? user.Playmode;
        var plans = new List<(ScoreType Type, string Mode, int Pages)>();
        foreach (var type in options.ScoreTypes)
        {
            foreach (var mode in options.ScoreModes)
            {
                var known = mode == countsMode ? KnownCount(user, type) : null;
                plans.Add((type, mode, ExpectedRequests(known, options.PageSize)));
            }
        }
        var mostPlayedPages = ExpectedRequests(user.BeatmapPlaycountsCount, options.PageSize);
        budget.Plan(plans.Sum(p => p.Pages) + mostPlayedPages);
        Report($"Found {user.Username} (id {user.Id}).");

        foreach (var (type, mode, pages) in plans)
        {
            var source = HarvestSource.From(type);
            var tracker = budget.Tracker(pages);
            try
            {
                var count = await PaginateAsync(
                    (offset, ct) => api.GetScoresAsync(user.Id, type, mode, options.PageSize, offset, ct),
                    page => { foreach (var score in page) aggregator.AddScore(source, score); },
                    options.PageSize,
                    tracker,
                    cancellationToken).ConfigureAwait(false);
                Report($"Scores {source} [{mode}]: {count}.");
            }
            catch (OsuApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            {
                // A list that the API does not offer for this profile/mode should not abort the harvest.
                tracker.RequestDone(moreExpected: false);
                tracker.Finish();
                Report($"Scores {source} [{mode}]: skipped ({ex.Message}).");
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
            budget.Tracker(mostPlayedPages),
            cancellationToken).ConfigureAwait(false);
        Report($"Most played: {played} difficulties.");

        var mapsets = aggregator.Build();
        budget.Complete();
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

    private static int? KnownCount(OsuUser user, ScoreType type) => type switch
    {
        ScoreType.Best => user.ScoresBestCount is { } best ? Math.Min(best, BestScoresCap) : null,
        ScoreType.Firsts => user.ScoresFirstCount,
        ScoreType.Pinned => user.ScoresPinnedCount,
        ScoreType.Recent => user.ScoresRecentCount,
        _ => null,
    };

    /// <summary>Pages with items plus the final empty page; one request when the size is unknown.</summary>
    internal static int ExpectedRequests(int? itemCount, int pageSize) =>
        itemCount is { } count ? (count + pageSize - 1) / pageSize + 1 : 1;

    /// <summary>Requests pages until an empty one comes back. Returns the number of items read.</summary>
    internal static async Task<int> PaginateAsync<T>(
        Func<int, CancellationToken, Task<IReadOnlyList<T>>> fetch,
        Action<IReadOnlyList<T>> consume,
        int pageSize,
        RequestBudget.ListTracker? tracker,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        var offset = 0;
        for (var page = 0; page < MaxPages; page++)
        {
            var items = await fetch(offset, cancellationToken).ConfigureAwait(false);
            tracker?.RequestDone(moreExpected: items.Count > 0);
            if (items.Count == 0)
                break;
            consume(items);
            offset += items.Count;
        }
        tracker?.Finish();
        return offset;
    }
}

/// <summary>
/// Turns "requests done / requests expected" into a progress fraction. Lists whose size is unknown
/// grow the estimate as they turn out longer, so the fraction can slow down but never goes backwards.
/// </summary>
internal sealed class RequestBudget
{
    private int _done;
    private int _total;
    private double _reported;

    public double Fraction
    {
        get
        {
            var raw = _total == 0 ? 0 : Math.Min(0.99, (double)_done / _total);
            _reported = Math.Max(_reported, raw);
            return _reported;
        }
    }

    public void Plan(int requests) => _total += requests;
    public void Done() => _done++;
    public void Complete() => _reported = 1;

    public ListTracker Tracker(int plannedRequests) => new(this, plannedRequests);

    internal sealed class ListTracker(RequestBudget budget, int planned)
    {
        private int _made;

        public void RequestDone(bool moreExpected)
        {
            _made++;
            budget._done++;
            // The list is longer than planned: there is at least one more request to come.
            if (moreExpected && _made >= planned)
                budget._total++;
        }

        /// <summary>A list that ended early (or failed) gives back the requests it no longer needs.</summary>
        public void Finish()
        {
            if (_made < planned)
                budget._total -= planned - _made;
        }
    }
}
