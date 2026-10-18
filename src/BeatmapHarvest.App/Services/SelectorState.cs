using BeatmapHarvest.Core.Selection;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// The selector's configuration per profile, kept app-wide so it survives navigation.
/// Each profile has its own selection; a profile opened for the first time starts with the filters of the last one used.
/// </summary>
public sealed class SelectorState
{
    private readonly Dictionary<long, SelectorConfig> _configs = [];
    private SelectorConfig? _last;

    public SelectorConfig For(long userId)
    {
        if (!_configs.TryGetValue(userId, out var config))
        {
            config = _last is null ? new SelectorConfig() : CopyFilters(_last);
            _configs[userId] = config;
        }
        _last = config;
        return config;
    }

    public void Set(long userId, SelectorConfig config)
    {
        _configs[userId] = config;
        _last = config;
    }

    public void Forget(long userId) => _configs.Remove(userId);

    /// <summary>Same filters and view options, no hand-picked maps (those are ids from another profile).</summary>
    private static SelectorConfig CopyFilters(SelectorConfig source) => new()
    {
        SelectionMode = SelectionMode.Filters,
        Sort = source.Sort,
        Filters = new SelectionFilters
        {
            Modes = [.. source.Filters.Modes],
            Statuses = [.. source.Filters.Statuses],
            Sources = [.. source.Filters.Sources],
            Stars = source.Filters.Stars,
            Length = source.Filters.Length,
            Plays = source.Filters.Plays,
            SkipOwned = source.Filters.SkipOwned,
            HideUnmatched = source.Filters.HideUnmatched,
        },
    };
}
