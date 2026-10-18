namespace BeatmapHarvest.Core.Osu;

/// <summary>osu! game modes, using the API's names.</summary>
public static class Ruleset
{
    public const string Osu = "osu";
    public const string Taiko = "taiko";
    public const string Fruits = "fruits";
    public const string Mania = "mania";

    public static IReadOnlyList<string> All { get; } = [Osu, Taiko, Fruits, Mania];

    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

/// <summary>Score lists available on a profile.</summary>
public enum ScoreType
{
    Best,
    Firsts,
    Pinned,
    Recent,
}

/// <summary>Where a beatmapset was found. Stored in <see cref="Harvesting.MapsetInfo.Sources"/>.</summary>
public static class HarvestSource
{
    public const string Best = "best";
    public const string Firsts = "firsts";
    public const string Pinned = "pinned";
    public const string Recent = "recent";
    public const string MostPlayed = "most_played";

    public static string From(ScoreType type) => type switch
    {
        ScoreType.Best => Best,
        ScoreType.Firsts => Firsts,
        ScoreType.Pinned => Pinned,
        ScoreType.Recent => Recent,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
