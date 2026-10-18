using System.Text.Json.Serialization;

namespace BeatmapHarvest.Core.Osu;

// Subsets of the osu! API v2 objects; only the fields this app uses.
// Reference: https://osu.ppy.sh/docs/#user, #beatmapset, #beatmap, #beatmapplaycount, #score

public sealed record OsuUser
{
    public long Id { get; init; }
    public string Username { get; init; } = "";
    public string? AvatarUrl { get; init; }
    public string? CountryCode { get; init; }

    /// <summary>The profile's default ruleset (osu, taiko, fruits, mania).</summary>
    public string? Playmode { get; init; }

    public OsuUserStatistics? Statistics { get; init; }
    public int? BeatmapPlaycountsCount { get; init; }

    // Sizes of the score lists in the requested (or default) ruleset; used to estimate progress.
    public int? ScoresBestCount { get; init; }
    public int? ScoresFirstCount { get; init; }
    public int? ScoresPinnedCount { get; init; }
    public int? ScoresRecentCount { get; init; }
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

/// <summary><c>GET /beatmapsets/{id}</c>: the set with all its difficulties and their attributes.</summary>
public sealed record OsuBeatmapsetDetails
{
    public long Id { get; init; }
    public string Artist { get; init; } = "";
    public string? ArtistUnicode { get; init; }
    public string Title { get; init; } = "";
    public string? TitleUnicode { get; init; }
    public string Creator { get; init; } = "";
    public string? Source { get; init; }
    public string? Tags { get; init; }
    public string? Status { get; init; }
    public double Bpm { get; init; }
    public long PlayCount { get; init; }
    public long FavouriteCount { get; init; }
    public DateTimeOffset? RankedDate { get; init; }
    public DateTimeOffset? SubmittedDate { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public string? PreviewUrl { get; init; }
    public bool Video { get; init; }
    public bool Storyboard { get; init; }
    public OsuCovers? Covers { get; init; }
    public IReadOnlyList<OsuBeatmapDetails> Beatmaps { get; init; } = [];
}

public sealed record OsuBeatmapDetails
{
    public long Id { get; init; }
    public string Version { get; init; } = "";
    public string Mode { get; init; } = Ruleset.Osu;
    public double DifficultyRating { get; init; }
    public int TotalLength { get; init; }
    public int HitLength { get; init; }
    public double Bpm { get; init; }
    public double Cs { get; init; }
    public double Ar { get; init; }

    /// <summary>Overall difficulty (OD).</summary>
    public double Accuracy { get; init; }

    /// <summary>HP drain.</summary>
    public double Drain { get; init; }

    public int CountCircles { get; init; }
    public int CountSliders { get; init; }
    public int CountSpinners { get; init; }
    public int? MaxCombo { get; init; }
    public long Playcount { get; init; }
    public long Passcount { get; init; }

    /// <summary>MD5 of the .osu file; what osu! collections refer to.</summary>
    public string? Checksum { get; init; }
}

internal sealed record OsuTokenResponse
{
    public string TokenType { get; init; } = "";
    public int ExpiresIn { get; init; }
    public string AccessToken { get; init; } = "";

    /// <summary>Only for the authorization-code and refresh-token grants.</summary>
    public string? RefreshToken { get; init; }
}
