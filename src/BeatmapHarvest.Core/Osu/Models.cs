using System.Text.Json.Serialization;

namespace BeatmapHarvest.Core.Osu;

// Subsets of the osu! API v2 objects; only the fields this app uses.
// Reference: https://osu.ppy.sh/docs/#user, #beatmapset, #beatmap, #beatmapplaycount, #score

public sealed record OsuUser
{
    public long Id { get; init; }
    public string Username { get; init; } = "";

    /// <summary>The profile's default ruleset (osu, taiko, fruits, mania).</summary>
    public string? Playmode { get; init; }

    public OsuUserStatistics? Statistics { get; init; }
    public int? BeatmapPlaycountsCount { get; init; }
}

public sealed record OsuUserStatistics
{
    /// <summary>Number of plays in the requested ruleset (not the number of maps).</summary>
    public long PlayCount { get; init; }
}

public sealed record OsuBeatmapset
{
    public long Id { get; init; }
    public string Artist { get; init; } = "";
    public string Title { get; init; } = "";
    public string Creator { get; init; } = "";
    public string? Status { get; init; }

    /// <summary>Protocol-relative URL, e.g. <c>//b.ppy.sh/preview/1.mp3</c>.</summary>
    public string? PreviewUrl { get; init; }

    public OsuCovers? Covers { get; init; }
}

public sealed record OsuCovers
{
    [JsonPropertyName("cover")] public string? Cover { get; init; }
    [JsonPropertyName("cover@2x")] public string? Cover2x { get; init; }
    [JsonPropertyName("card")] public string? Card { get; init; }
    [JsonPropertyName("card@2x")] public string? Card2x { get; init; }
    [JsonPropertyName("list")] public string? List { get; init; }
    [JsonPropertyName("list@2x")] public string? List2x { get; init; }
}

public sealed record OsuBeatmap
{
    public long Id { get; init; }
    public long BeatmapsetId { get; init; }
    public string Version { get; init; } = "";
    public string Mode { get; init; } = Ruleset.Osu;
    public double DifficultyRating { get; init; }

    /// <summary>Length in seconds.</summary>
    public int TotalLength { get; init; }

    public string? Status { get; init; }
}

/// <summary>An entry of <c>/users/{id}/beatmapsets/most_played</c>: one difficulty and how often the user played it.</summary>
public sealed record OsuBeatmapPlaycount
{
    public long BeatmapId { get; init; }
    public int Count { get; init; }
    public OsuBeatmap? Beatmap { get; init; }
    public OsuBeatmapset? Beatmapset { get; init; }
}

public sealed record OsuScore
{
    public OsuBeatmap? Beatmap { get; init; }
    public OsuBeatmapset? Beatmapset { get; init; }
}

internal sealed record OsuTokenResponse
{
    public string TokenType { get; init; } = "";
    public int ExpiresIn { get; init; }
    public string AccessToken { get; init; } = "";
}
