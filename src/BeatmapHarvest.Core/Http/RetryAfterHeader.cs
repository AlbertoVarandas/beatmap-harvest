using System.Net.Http.Headers;

namespace BeatmapHarvest.Core.Http;

internal static class RetryAfterHeader
{
    /// <summary>The wait requested by a 429/503 response, or null when the server gave none.</summary>
    public static TimeSpan? Get(HttpResponseHeaders headers, DateTimeOffset now)
    {
        if (headers.RetryAfter?.Delta is { } delta)
            return delta;
        if (headers.RetryAfter?.Date is { } date)
            return date > now ? date - now : TimeSpan.Zero;
        return null;
    }
}
