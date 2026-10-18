using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Selection;

/// <summary>
/// Decides which beatmapsets are selected. Mode, stars, length and plays are checked per difficulty:
/// a set passes when at least one difficulty meets all of them at once.
/// </summary>
public static class SelectionEngine
{
    public static bool MatchesDifficulty(DifficultyInfo difficulty, SelectionFilters filters) =>
        (filters.Modes.Count == 0 || filters.Modes.Contains(difficulty.Mode))
        && filters.Stars.Contains(difficulty.Stars)
        && filters.Length.Contains(difficulty.LengthSeconds)
        && filters.Plays.Contains(difficulty.Plays ?? 0);

    /// <summary>Whether the set passes the selection filters (search is not one of them).</summary>
    public static bool Matches(MapsetInfo mapset, SelectionFilters filters, MapOwnership ownership) =>
        MatchesLibrary(filters, ownership)
        && (filters.Statuses.Count == 0 || (mapset.Status is not null && filters.Statuses.Contains(mapset.Status)))
        && (filters.Sources.Count == 0 || mapset.Sources.Any(filters.Sources.Contains))
        && mapset.Difficulties.Any(d => MatchesDifficulty(d, filters));

    /// <summary>A Library filter, when set, decides alone; otherwise SkipOwned leaves out anything already on disk.</summary>
    private static bool MatchesLibrary(SelectionFilters filters, MapOwnership ownership) =>
        filters.Library.Count > 0
            ? filters.Library.Contains(ownership)
            : !(filters.SkipOwned && ownership != MapOwnership.None);

    public static bool Matches(MapsetInfo mapset, SelectionFilters filters, bool owned) =>
        Matches(mapset, filters, owned ? MapOwnership.InGame : MapOwnership.None);

    public static bool MatchesSearch(MapsetInfo mapset, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        var terms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.All(term =>
            mapset.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            || mapset.Artist.Contains(term, StringComparison.OrdinalIgnoreCase)
            || mapset.Creator.Contains(term, StringComparison.OrdinalIgnoreCase)
            || mapset.Difficulties.Any(d => d.Version.Contains(term, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool IsSelected(MapsetInfo mapset, SelectorConfig config, MapOwnership ownership) => config.SelectionMode switch
    {
        SelectionMode.Custom => config.Included.Contains(mapset.Id),
        _ => config.Included.Contains(mapset.Id)
             || (!config.Excluded.Contains(mapset.Id) && Matches(mapset, config.Filters, ownership)),
    };

    public static bool IsSelected(MapsetInfo mapset, SelectorConfig config, bool owned) =>
        IsSelected(mapset, config, owned ? MapOwnership.InGame : MapOwnership.None);

    /// <summary>Flips one set's selection, recording it as an exception to the filters when needed.</summary>
    public static void Toggle(MapsetInfo mapset, SelectorConfig config, MapOwnership ownership)
    {
        var id = mapset.Id;
        if (config.SelectionMode == SelectionMode.Custom)
        {
            if (!config.Included.Remove(id))
                config.Included.Add(id);
            return;
        }

        var matches = Matches(mapset, config.Filters, ownership);
        if (IsSelected(mapset, config, ownership))
        {
            config.Included.Remove(id);
            if (matches)
                config.Excluded.Add(id);
        }
        else
        {
            config.Excluded.Remove(id);
            if (!matches)
                config.Included.Add(id);
        }
    }

    public static void Toggle(MapsetInfo mapset, SelectorConfig config, bool owned) =>
        Toggle(mapset, config, owned ? MapOwnership.InGame : MapOwnership.None);

    /// <summary>Changes mode keeping exactly the same sets selected.</summary>
    public static void SwitchMode(SelectorConfig config, SelectionMode mode, IEnumerable<MapsetInfo> mapsets, OwnershipIndex ownership)
    {
        if (config.SelectionMode == mode)
            return;

        var all = mapsets.ToList();
        var selected = all.Where(m => IsSelected(m, config, ownership.Of(m.Id))).Select(m => m.Id).ToHashSet();
        config.Included.Clear();
        config.Excluded.Clear();
        config.SelectionMode = mode;

        if (mode == SelectionMode.Custom)
        {
            config.Included.UnionWith(selected);
            return;
        }

        foreach (var m in all)
        {
            var matches = Matches(m, config.Filters, ownership.Of(m.Id));
            if (selected.Contains(m.Id) && !matches)
                config.Included.Add(m.Id);
            else if (!selected.Contains(m.Id) && matches)
                config.Excluded.Add(m.Id);
        }
    }

    public static void SwitchMode(SelectorConfig config, SelectionMode mode, IEnumerable<MapsetInfo> mapsets, IReadOnlySet<long> owned) =>
        SwitchMode(config, mode, mapsets, new OwnershipIndex(owned, new HashSet<long>()));

    public static IReadOnlyList<long> SelectedIds(IEnumerable<MapsetInfo> mapsets, SelectorConfig config, OwnershipIndex ownership) =>
        mapsets.Where(m => IsSelected(m, config, ownership.Of(m.Id))).Select(m => m.Id).ToList();

    public static IReadOnlyList<long> SelectedIds(IEnumerable<MapsetInfo> mapsets, SelectorConfig config, IReadOnlySet<long> owned) =>
        SelectedIds(mapsets, config, new OwnershipIndex(owned, new HashSet<long>()));
}
