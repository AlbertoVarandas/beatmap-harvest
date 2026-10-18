using BeatmapHarvest.Core.Harvesting;

namespace BeatmapHarvest.Core.Selection;

/// <summary>Orders for the selector list, by <see cref="SelectorConfig.Sort"/> key.</summary>
public static class MapsetSort
{
    public const string Plays = "plays";
    public const string Artist = "artist";
    public const string Title = "title";
    public const string Stars = "stars";
    public const string Length = "length";
    public const string Mapper = "mapper";

    public static IReadOnlyList<string> All { get; } = [Plays, Artist, Title, Stars, Length, Mapper];

    /// <summary>Numbers sort highest first, text A–Z; ties fall back to artist, title and id. Unknown keys sort by plays.</summary>
    public static IEnumerable<MapsetInfo> Apply(IEnumerable<MapsetInfo> mapsets, string? sort)
    {
        var ordered = sort switch
        {
            Artist => mapsets.OrderBy(m => m.Artist, StringComparer.CurrentCultureIgnoreCase),
            Title => mapsets.OrderBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase),
            Mapper => mapsets.OrderBy(m => m.Creator, StringComparer.CurrentCultureIgnoreCase),
            Stars => mapsets.OrderByDescending(MaxStars),
            Length => mapsets.OrderByDescending(m => m.LengthSeconds),
            _ => mapsets.OrderByDescending(m => m.TotalPlays),
        };
        return ordered
            .ThenBy(m => m.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.Id);
    }

    private static double MaxStars(MapsetInfo mapset) => mapset.Difficulties.Count == 0 ? 0 : mapset.Difficulties.Max(d => d.Stars);
}
