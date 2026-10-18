namespace BeatmapHarvest.Core.Harvesting;

public sealed record MostPlayedEntry(MapsetInfo Mapset, DifficultyInfo Difficulty);

/// <summary>Totals shown after a harvest.</summary>
public sealed record HarvestSummary(
    int Mapsets,
    int Difficulties,
    long Plays,
    IReadOnlyDictionary<string, int> DifficultiesByMode,
    double MinStars,
    double MaxStars,
    double AverageStars,
    TimeSpan TotalSongLength,
    IReadOnlyList<MostPlayedEntry> MostPlayed)
{
    public static HarvestSummary From(IReadOnlyList<MapsetInfo> mapsets, int top = 10)
    {
        var diffs = mapsets.SelectMany(m => m.Difficulties.Select(d => (Mapset: m, Diff: d))).ToList();
        var stars = diffs.Select(x => x.Diff.Stars).ToList();

        return new HarvestSummary(
            Mapsets: mapsets.Count,
            Difficulties: diffs.Count,
            Plays: diffs.Sum(x => (long)(x.Diff.Plays ?? 0)),
            DifficultiesByMode: diffs
                .GroupBy(x => x.Diff.Mode, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            MinStars: stars.Count == 0 ? 0 : stars.Min(),
            MaxStars: stars.Count == 0 ? 0 : stars.Max(),
            AverageStars: stars.Count == 0 ? 0 : Math.Round(stars.Average(), 2),
            TotalSongLength: TimeSpan.FromSeconds(mapsets.Sum(m => (long)m.LengthSeconds)),
            MostPlayed: diffs
                .Where(x => x.Diff.Plays is > 0)
                .OrderByDescending(x => x.Diff.Plays)
                .Take(top)
                .Select(x => new MostPlayedEntry(x.Mapset, x.Diff))
                .ToList());
    }
}
