using System.Net;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests.Harvesting;

public class MapsetAggregatorTests
{
    [Fact]
    public void Merges_sources_and_keeps_plays_from_most_played()
    {
        var aggregator = new MapsetAggregator();
        var set = Samples.Set(10);
        var map = Samples.Map(100, 10);

        aggregator.AddMostPlayed(new OsuBeatmapPlaycount { BeatmapId = 100, Count = 7, Beatmap = map, Beatmapset = set });
        aggregator.AddScore(HarvestSource.Best, new OsuScore { Beatmap = map, Beatmapset = set });

        var info = Assert.Single(aggregator.Build());
        Assert.Equal([HarvestSource.Best, HarvestSource.MostPlayed], info.Sources);
        var diff = Assert.Single(info.Difficulties);
        Assert.Equal(7, diff.Plays);
        Assert.Equal("https://b.ppy.sh/preview/10.mp3", info.Preview);
        Assert.Equal("https://osu.ppy.sh/beatmapsets/10", info.Url);
    }

    [Fact]
    public void Score_only_difficulties_have_no_play_count()
    {
        var aggregator = new MapsetAggregator();
        aggregator.AddScore(HarvestSource.Recent, new OsuScore { Beatmap = Samples.Map(1, 5), Beatmapset = Samples.Set(5) });

        Assert.Null(Assert.Single(Assert.Single(aggregator.Build()).Difficulties).Plays);
    }

    [Fact]
    public void Sorts_by_artist_then_title_and_uses_longest_difficulty_for_length()
    {
        var aggregator = new MapsetAggregator();
        aggregator.Add(HarvestSource.Best, Samples.Set(1, "b", "x"), Samples.Map(11, 1, length: 60), null);
        aggregator.Add(HarvestSource.Best, Samples.Set(2, "A", "z"), Samples.Map(21, 2, length: 60), null);
        aggregator.Add(HarvestSource.Best, Samples.Set(3, "a", "y"), Samples.Map(31, 3, length: 90), null);
        aggregator.Add(HarvestSource.Best, Samples.Set(3, "a", "y"), Samples.Map(32, 3, length: 200), null);

        var result = aggregator.Build();

        Assert.Equal([3L, 2L, 1L], result.Select(m => m.Id));
        Assert.Equal(200, result[0].LengthSeconds);
    }

    [Fact]
    public void Ignores_entries_without_set_metadata()
    {
        var aggregator = new MapsetAggregator();
        aggregator.Add(HarvestSource.Best, null, Samples.Map(1, 99), null);
        Assert.Empty(aggregator.Build());
    }
}

public class ProfileHarvesterTests
{
    private sealed class FakeSource : IOsuDataSource
    {
        public List<string> Calls { get; } = [];

        public Task<OsuUser> GetUserAsync(UserReference user, CancellationToken cancellationToken) =>
            Task.FromResult(new OsuUser { Id = 1, Username = "tester", Playmode = Ruleset.Mania });

        public Task<IReadOnlyList<OsuScore>> GetScoresAsync(long userId, ScoreType type, string mode, int limit, int offset, CancellationToken cancellationToken)
        {
            Calls.Add($"{type}:{mode}:{offset}");
            if (type == ScoreType.Pinned)
                throw new OsuApiException("not available", HttpStatusCode.NotFound);
            IReadOnlyList<OsuScore> page = type == ScoreType.Best && mode == Ruleset.Mania && offset == 0
                ? [new OsuScore { Beatmapset = Samples.Set(500), Beatmap = Samples.Map(5000, 500) }]
                : [];
            return Task.FromResult(page);
        }

        public Task<IReadOnlyList<OsuBeatmapPlaycount>> GetMostPlayedAsync(long userId, int limit, int offset, CancellationToken cancellationToken)
        {
            Calls.Add($"most_played:{offset}");
            // Two full pages of `limit` items, then empty.
            IReadOnlyList<OsuBeatmapPlaycount> page = offset >= 2 * limit
                ? []
                : Enumerable.Range(offset, limit)
                    .Select(i => new OsuBeatmapPlaycount { BeatmapId = i, Count = 1, Beatmap = Samples.Map(i, 1000 + (i / 2)), Beatmapset = Samples.Set(1000 + (i / 2)) })
                    .ToList();
            return Task.FromResult(page);
        }
    }

    [Fact]
    public async Task Paginates_until_empty_and_skips_unavailable_lists()
    {
        var source = new FakeSource();
        var harvester = new ProfileHarvester(source);

        var result = await harvester.HarvestAsync(
            new UserReference("1", null),
            new HarvestOptions { PageSize = 3 },
            progress: null,
            CancellationToken.None);

        Assert.Equal("tester", result.Username);
        Assert.Equal(["most_played:0", "most_played:3", "most_played:6"], source.Calls.Where(c => c.StartsWith("most_played", StringComparison.Ordinal)));
        Assert.Contains("Pinned:osu:0", source.Calls);
        // 6 difficulties → 3 sets from most played, plus 1 from best scores.
        Assert.Equal(4, result.Mapsets.Count);
        Assert.Contains(result.Mapsets, m => m.Id == 500 && m.Sources.Contains(HarvestSource.Best));
    }
}

public class HarvestSummaryTests
{
    [Fact]
    public void Computes_totals()
    {
        MapsetInfo[] mapsets =
        [
            Samples.Mapset(1, Samples.Diff(Ruleset.Mania, 2, 100, 10), Samples.Diff(Ruleset.Mania, 4, 150, null)),
            Samples.Mapset(2, Samples.Diff(Ruleset.Osu, 5, 60, 3)),
        ];

        var summary = HarvestSummary.From(mapsets);

        Assert.Equal(2, summary.Mapsets);
        Assert.Equal(3, summary.Difficulties);
        Assert.Equal(13, summary.Plays);
        Assert.Equal(2, summary.DifficultiesByMode[Ruleset.Mania]);
        Assert.Equal(2, summary.MinStars);
        Assert.Equal(5, summary.MaxStars);
        Assert.Equal(TimeSpan.FromSeconds(210), summary.TotalSongLength);
        Assert.Equal(10, summary.MostPlayed[0].Difficulty.Plays);
    }
}
