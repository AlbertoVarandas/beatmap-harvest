using BeatmapHarvest.Core.Selection;

namespace BeatmapHarvest.App.Services;

/// <summary>The selector's configuration, kept app-wide so it survives navigation.</summary>
public sealed class SelectorState
{
    public SelectorConfig Config { get; set; } = new();
}
