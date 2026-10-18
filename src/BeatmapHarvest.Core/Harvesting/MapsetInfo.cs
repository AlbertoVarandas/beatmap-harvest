using System.Text.Json.Serialization;

namespace BeatmapHarvest.Core.Harvesting;

/// <summary>A beatmapset linked to the profile, merged from every source it appeared in.</summary>
public sealed record MapsetInfo
{
    public long Id { get; init; }
    public string Artist { get; init; } = "";
    public string Title { get; init; } = "";
    public string Creator { get; init; } = "";
    public string? Status { get; init; }
    public string Url { get; init; } = "";
    public string? Cover { get; init; }
    public string? Card { get; init; }
    public string? Preview { get; init; }

    /// <summary>best, firsts, pinned, recent and/or most_played.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    public IReadOnlyList<DifficultyInfo> Difficulties { get; init; } = [];

    /// <summary>Song length: the longest difficulty, so one song is not counted several times.</summary>
    [JsonIgnore]
    public int LengthSeconds => Difficulties.Count == 0 ? 0 : Difficulties.Max(d => d.LengthSeconds);

    [JsonIgnore]
    public int TotalPlays => Difficulties.Sum(d => d.Plays ?? 0);
}

public sealed record DifficultyInfo
{
    public long Id { get; init; }
    public string Version { get; init; } = "";
    public string Mode { get; init; } = "";
    public double Stars { get; init; }
    public int LengthSeconds { get; init; }

    /// <summary>The user's play count; only most_played provides it, so it is null for score-only difficulties.</summary>
    public int? Plays { get; init; }
}

/// <summary>Contents of <c>beatmap_info.json</c> for one profile.</summary>
public sealed record HarvestResult
{
    public long UserId { get; init; }
    public string Username { get; init; } = "";
    public string? DefaultMode { get; init; }
    public DateTimeOffset HarvestedAt { get; init; }
    public IReadOnlyList<MapsetInfo> Mapsets { get; init; } = [];
}
