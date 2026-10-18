namespace BeatmapHarvest.Core.Selection;

public enum SelectionMode
{
    /// <summary>Selected = (passes the filters and not excluded) or included.</summary>
    Filters,

    /// <summary>Selected = included. Filters only narrow what is shown.</summary>
    Custom,
}

public sealed class SelectionFilters
{
    /// <summary>Empty = every mode.</summary>
    public HashSet<string> Modes { get; init; } = [];

    /// <summary>Empty = every status (ranked, loved, …).</summary>
    public HashSet<string> Statuses { get; init; } = [];

    /// <summary>Empty = every source (best, most_played, …).</summary>
    public HashSet<string> Sources { get; init; } = [];

    public NumericRange Stars { get; set; }

    /// <summary>Seconds.</summary>
    public NumericRange Length { get; set; }

    /// <summary>The user's own plays of a difficulty.</summary>
    public NumericRange Plays { get; set; }

    /// <summary>View-only: narrows what is shown, never what is selected.</summary>
    public string Search { get; set; } = "";

    public bool SkipOwned { get; set; } = true;

    /// <summary>Only maps in these states (in osu!, downloaded, not owned). Empty = any; when set, <see cref="SkipOwned"/> is ignored.</summary>
    public HashSet<Library.MapOwnership> Library { get; init; } = [];

    /// <summary>View-only.</summary>
    public bool OnlySelected { get; set; }

    /// <summary>View-only: hide maps and difficulties outside the filters instead of dimming them.</summary>
    public bool HideUnmatched { get; set; }
}

/// <summary>The selector's state; can be saved to and loaded from a JSON file.</summary>
public sealed class SelectorConfig
{
    public int BeatmapHarvestSelector { get; init; } = 1;
    public SelectionMode SelectionMode { get; set; } = SelectionMode.Filters;
    public string Sort { get; set; } = "plays";
    public SelectionFilters Filters { get; init; } = new();
    public HashSet<long> Included { get; init; } = [];
    public HashSet<long> Excluded { get; init; } = [];
}
