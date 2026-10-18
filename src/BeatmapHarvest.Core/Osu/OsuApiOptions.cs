namespace BeatmapHarvest.Core.Osu;

public sealed class OsuApiOptions
{
    public Uri BaseAddress { get; init; } = new("https://osu.ppy.sh/");

    /// <summary>The docs ask for at most 60 requests per minute, so one per second.</summary>
    public TimeSpan MinRequestInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Retries of the same request after HTTP 429.</summary>
    public int MaxRetries { get; init; } = 5;

    /// <summary>Wait after a 429 without Retry-After; grows linearly with each attempt (15 s, 30 s, 45 s…).</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Refresh the token this long before it expires.</summary>
    public TimeSpan TokenExpiryMargin { get; init; } = TimeSpan.FromMinutes(5);
}
