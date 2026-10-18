using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Harvesting;

/// <summary>
/// Merges score and most-played entries into one list of beatmapsets.
/// Keyed by beatmapset id; difficulties keyed by beatmap id.
/// </summary>
public sealed class MapsetAggregator
{
    private readonly Dictionary<long, Entry> _sets = [];

    public int Count => _sets.Count;

    public void AddScore(string source, OsuScore score) => Add(source, score.Beatmapset, score.Beatmap, plays: null);

    public void AddMostPlayed(OsuBeatmapPlaycount entry) =>
        Add(HarvestSource.MostPlayed, entry.Beatmapset, entry.Beatmap, entry.Count);

    public void Add(string source, OsuBeatmapset? beatmapset, OsuBeatmap? beatmap, int? plays)
    {
        var setId = beatmapset?.Id ?? beatmap?.BeatmapsetId ?? 0;
        if (setId <= 0)
            return;

        if (!_sets.TryGetValue(setId, out var entry))
        {
            // Without the set's metadata there is nothing useful to show.
            if (beatmapset is null)
                return;
            entry = new Entry(beatmapset);
            _sets.Add(setId, entry);
        }

        entry.Sources.Add(source);
        if (beatmap is null)
            return;

        var diff = new DifficultyInfo
        {
            Id = beatmap.Id,
            Version = beatmap.Version,
            Mode = beatmap.Mode,
            Stars = Math.Round(beatmap.DifficultyRating, 2),
            LengthSeconds = beatmap.TotalLength,
            Plays = plays,
        };
        if (entry.Difficulties.TryGetValue(beatmap.Id, out var existing))
            diff = diff with { Plays = plays ?? existing.Plays };
        entry.Difficulties[beatmap.Id] = diff;
    }

    /// <summary>Sets sorted by artist, then title.</summary>
    public IReadOnlyList<MapsetInfo> Build() =>
        _sets.Values
            .Select(e => e.ToInfo())
            .OrderBy(m => m.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id)
            .ToList();

    private static readonly string[] SourceOrder =
        [HarvestSource.Best, HarvestSource.Firsts, HarvestSource.Pinned, HarvestSource.Recent, HarvestSource.MostPlayed];

    private sealed class Entry(OsuBeatmapset set)
    {
        public HashSet<string> Sources { get; } = [];
        public Dictionary<long, DifficultyInfo> Difficulties { get; } = [];

        public MapsetInfo ToInfo() => new()
        {
            Id = set.Id,
            Artist = set.Artist,
            Title = set.Title,
            Creator = set.Creator,
            Status = set.Status,
            Url = $"https://osu.ppy.sh/beatmapsets/{set.Id}",
            Cover = set.Covers?.List2x ?? set.Covers?.List,
            Card = set.Covers?.Card2x ?? set.Covers?.Card,
            Preview = AbsoluteUrl(set.PreviewUrl),
            Sources = SourceOrder.Where(Sources.Contains).Concat(Sources.Except(SourceOrder).Order(StringComparer.Ordinal)).ToList(),
            Difficulties = Difficulties.Values
                .OrderBy(d => d.Mode, StringComparer.Ordinal)
                .ThenBy(d => d.Stars)
                .ToList(),
        };
    }

    // preview_url comes protocol-relative ("//b.ppy.sh/preview/1.mp3").
    internal static string? AbsoluteUrl(string? url) =>
        url is not null && url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
}
