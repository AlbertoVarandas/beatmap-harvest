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

    /// <summary>A map attribute (CS, AR, BPM…): whole numbers without decimals, otherwise one decimal.</summary>
    public static string Number(double value) => value.ToString("0.#", CultureInfo.CurrentCulture);

    /// <summary>A 0–1 fraction as a CSS-safe percentage, e.g. <c>42.5%</c>.</summary>
    public static string Percent(double fraction, int decimals = 1) =>
        (Math.Clamp(fraction, 0, 1) * 100).ToString(decimals == 0 ? "0" : "0.#", CultureInfo.InvariantCulture) + "%";

    public static string Status(string? status) => status switch
    {
        null or "" => "unknown",
        "wip" => "WIP",
        _ => status,
    };
}
