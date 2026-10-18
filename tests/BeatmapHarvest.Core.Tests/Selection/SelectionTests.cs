using System.Text.Json;
using BeatmapHarvest.Core.Json;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Selection;

namespace BeatmapHarvest.Core.Tests.Selection;

public class SelectionEngineTests
{
    [Fact]
    public void A_set_matches_only_when_one_difficulty_meets_every_filter_at_once()
    {
        // One easy mania diff and one hard osu diff: "mania and ≥ 4 stars" must not match.
        var mapset = Samples.Mapset(1, Samples.Diff(Ruleset.Mania, 2), Samples.Diff(Ruleset.Osu, 5));
        var filters = new SelectionFilters { Modes = [Ruleset.Mania], Stars = new NumericRange(4, null) };

        Assert.False(SelectionEngine.Matches(mapset, filters, owned: false));

        filters.Stars = new NumericRange(1, 3);
        Assert.True(SelectionEngine.Matches(mapset, filters, owned: false));
    }

    [Fact]
    public void Owned_sets_are_skipped_when_requested()
    {
        var mapset = Samples.Mapset(1, Samples.Diff());
        Assert.False(SelectionEngine.Matches(mapset, new SelectionFilters { SkipOwned = true }, owned: true));
        Assert.True(SelectionEngine.Matches(mapset, new SelectionFilters { SkipOwned = false }, owned: true));
    }

    [Fact]
    public void Missing_play_counts_count_as_zero()
    {
        var mapset = Samples.Mapset(1, Samples.Diff(plays: null));
        Assert.False(SelectionEngine.Matches(mapset, new SelectionFilters { Plays = new NumericRange(1, null) }, owned: false));
    }

    [Fact]
    public void Toggle_records_exceptions_to_the_filters()
    {
        var hard = Samples.Mapset(1, Samples.Diff(stars: 6));
        var easy = Samples.Mapset(2, Samples.Diff(stars: 1));
        var config = new SelectorConfig { Filters = { Stars = new NumericRange(5, null) } };

        Assert.True(SelectionEngine.IsSelected(hard, config, false));
        Assert.False(SelectionEngine.IsSelected(easy, config, false));

        SelectionEngine.Toggle(hard, config, false);
        SelectionEngine.Toggle(easy, config, false);

        Assert.Equal([1L], config.Excluded);
        Assert.Equal([2L], config.Included);
        Assert.False(SelectionEngine.IsSelected(hard, config, false));
        Assert.True(SelectionEngine.IsSelected(easy, config, false));

        SelectionEngine.Toggle(easy, config, false);
        Assert.Empty(config.Included);
    }

    [Fact]
    public void Switching_mode_keeps_the_same_selection()
    {
        var sets = new[]
        {
            Samples.Mapset(1, Samples.Diff(stars: 6)),
            Samples.Mapset(2, Samples.Diff(stars: 1)),
            Samples.Mapset(3, Samples.Diff(stars: 7)),
        };
        var config = new SelectorConfig { Filters = { Stars = new NumericRange(5, null) }, Included = { 2 }, Excluded = { 3 } };
        var owned = new HashSet<long>();
        var before = SelectionEngine.SelectedIds(sets, config, owned);

        SelectionEngine.SwitchMode(config, SelectionMode.Custom, sets, owned);
        Assert.Equal(before, SelectionEngine.SelectedIds(sets, config, owned));
        Assert.Equal([1L, 2L], config.Included.Order());

        SelectionEngine.SwitchMode(config, SelectionMode.Filters, sets, owned);
        Assert.Equal(before, SelectionEngine.SelectedIds(sets, config, owned));
        Assert.Equal([2L], config.Included);
        Assert.Equal([3L], config.Excluded);
    }

    [Theory]
    [InlineData("song 1", true)]
    [InlineData("MAPPER", true)]
    [InlineData("normal", true)]
    [InlineData("artist missing", false)]
    [InlineData("", true)]
    public void Search_matches_every_term(string search, bool expected)
    {
        Assert.Equal(expected, SelectionEngine.MatchesSearch(Samples.Mapset(1, Samples.Diff()), search));
    }

    [Fact]
    public void Config_round_trips_with_open_ranges_as_null()
    {
        var config = new SelectorConfig
        {
            SelectionMode = SelectionMode.Custom,
            Filters = { Modes = { Ruleset.Mania }, Stars = new NumericRange(3, null) },
            Included = { 318207 },
        };

        var json = JsonSerializer.Serialize(config, JsonDefaults.Files);
        var loaded = JsonSerializer.Deserialize<SelectorConfig>(json, JsonDefaults.Files)!;

        Assert.Contains("\"stars\": [\n      3,\n      null\n    ]", json.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("\"selection_mode\": \"custom\"", json, StringComparison.Ordinal);
        Assert.Equal(SelectionMode.Custom, loaded.SelectionMode);
        Assert.Equal(new NumericRange(3, null), loaded.Filters.Stars);
        Assert.Equal(NumericRange.Any, loaded.Filters.Length);
        Assert.Contains(318207L, loaded.Included);
        Assert.Contains(Ruleset.Mania, loaded.Filters.Modes);
    }
}
