using System.Globalization;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Components;

/// <summary>Display text shared by the pages.</summary>
internal static class Format
{
    public static string Mode(string mode) => mode switch
    {
        Ruleset.Osu => "osu!",
        Ruleset.Taiko => "taiko",
        Ruleset.Fruits => "catch",
        Ruleset.Mania => "mania",
        _ => mode,
    };

    /// <summary>Hours and minutes, e.g. <c>3h07m</c>.</summary>
    public static string Duration(TimeSpan t) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}h{t.Minutes:00}m");

    /// <summary>Song length, e.g. <c>2:05</c>.</summary>
    public static string Length(double seconds)
    {
        var s = (int)Math.Round(seconds);
        return string.Create(CultureInfo.InvariantCulture, $"{s / 60}:{s % 60:00}");
    }

    public static string Stars(double stars) => stars.ToString("0.00", CultureInfo.CurrentCulture);

    public static string Status(string? status) => status switch
    {
        null or "" => "unknown",
        "wip" => "WIP",
        _ => status,
    };
}
