using BeatmapHarvest.Core.Display;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Selection;

namespace BeatmapHarvest.Core.Tests.Selection;

public class StarColorTests
{
    [Theory]
    [InlineData(0.1, "#4290fb")]
    [InlineData(2, "#4fffd5")]
    [InlineData(4.2, "#ff8068")]
    [InlineData(7.7, "#18158e")]
    [InlineData(9, "#000000")]
    [InlineData(12.5, "#000000")]
    [InlineData(0.05, "#aaaaaa")]
    [InlineData(0, "#aaaaaa")]
    public void Stops_and_ends_use_the_exact_colours(double stars, string expected)
    {
        Assert.Equal(expected, StarColor.For(stars));
    }

    [Fact]
    public void Between_stops_the_gradient_is_gamma_corrected()
    {
        // Halfway from #ff8068 to #ff4e6f: red stays, green falls, blue rises; gamma 2.2 keeps it brighter than linear.
        var mid = StarColor.For(4.55);
        var g = Convert.ToInt32(mid.Substring(3, 2), 16);
        var b = Convert.ToInt32(mid.Substring(5, 2), 16);

        Assert.StartsWith("#ff", mid, StringComparison.Ordinal);
        Assert.InRange(g, 0x68, 0x6c); // linear would be 0x67
        Assert.InRange(b, 0x6b, 0x6d);
    }

    [Fact]
    public void Text_turns_yellow_on_dark_colours()
    {
        Assert.Equal("#000000", StarColor.TextFor(3));
        Assert.Equal("#ffd966", StarColor.TextFor(7));
    }
}

public class SliderRangeTests
{
    private static readonly SliderRange Stars = new(0, 10);

    [Fact]
    public void Thumbs_at_the_ends_mean_no_limit()
    {
        Assert.Equal(NumericRange.Any, Stars.ToRange(0, 10));
        Assert.Equal(new NumericRange(2.5, null), Stars.ToRange(2.5, 10));
        Assert.Equal(new NumericRange(null, 6), Stars.ToRange(0, 6));
    }

    [Fact]
    public void Crossed_thumbs_are_swapped()
    {
        Assert.Equal(new NumericRange(3, 7), Stars.ToRange(7, 3));
    }

    [Fact]
    public void Ranges_map_back_to_thumb_positions()
    {
        Assert.Equal((0d, 10d), Stars.FromRange(NumericRange.Any));
        Assert.Equal((4d, 10d), Stars.FromRange(new NumericRange(4, 15)));
        Assert.Equal((0d, 5d), Stars.FromRange(new NumericRange(null, 5)));
    }
}

public class MapsetSortTests
{
    private static MapsetInfo Set(long id, string artist, string title, string creator, double stars, int length, int plays) =>
        Samples.Mapset(id, Samples.Diff(Ruleset.Osu, stars, length, plays)) with { Artist = artist, Title = title, Creator = creator };

    private static readonly MapsetInfo[] Sets =
    [
        Set(1, "b", "z", "m2", 3, 100, 5),
        Set(2, "a", "y", "m3", 6, 300, 1),
        Set(3, "c", "x", "m1", 1, 200, 9),
    ];

    [Theory]
    [InlineData(MapsetSort.Plays, new long[] { 3, 1, 2 })]
    [InlineData(MapsetSort.Artist, new long[] { 2, 1, 3 })]
    [InlineData(MapsetSort.Title, new long[] { 3, 2, 1 })]
    [InlineData(MapsetSort.Mapper, new long[] { 3, 1, 2 })]
    [InlineData(MapsetSort.Stars, new long[] { 2, 1, 3 })]
    [InlineData(MapsetSort.Length, new long[] { 2, 3, 1 })]
    [InlineData("unknown", new long[] { 3, 1, 2 })]
    public void Sorts_by_key(string sort, long[] expected)
    {
        Assert.Equal(expected, MapsetSort.Apply(Sets, sort).Select(m => m.Id));
    }

    [Fact]
    public void Ties_fall_back_to_artist()
    {
        var sets = new[] { Set(1, "b", "t", "m", 2, 60, 3), Set(2, "a", "t", "m", 2, 60, 3) };
        Assert.Equal([2L, 1L], MapsetSort.Apply(sets, MapsetSort.Plays).Select(m => m.Id));
    }
}
