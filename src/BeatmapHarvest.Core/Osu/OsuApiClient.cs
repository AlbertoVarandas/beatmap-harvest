using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BeatmapHarvest.Core.Http;
using BeatmapHarvest.Core.Json;

namespace BeatmapHarvest.Core.Osu;

/// <summary>The profile data this app needs. Abstracted so the source can change (e.g. a proxy backend) and for tests.</summary>
public interface IOsuDataSource
{
    Task<OsuUser> GetUserAsync(UserReference user, CancellationToken cancellationToken);

    Task<IReadOnlyList<OsuScore>> GetScoresAsync(long userId, ScoreType type, string mode, int limit, int offset, CancellationToken cancellationToken);

    Task<IReadOnlyList<OsuBeatmapPlaycount>> GetMostPlayedAsync(long userId, int limit, int offset, CancellationToken cancellationToken);

    Task<OsuBeatmapsetDetails> GetBeatmapsetAsync(long beatmapsetId, CancellationToken cancellationToken);
}

/// <summary>
/// osu! API v2 client. Serialises requests at <see cref="OsuApiOptions.MinRequestInterval"/>,
/// retries the same request on 429 and refreshes the token once on 401.
/// </summary>
public sealed class OsuApiClient(HttpClient http, IAccessTokenProvider tokens, OsuApiOptions options, TimeProvider time)
    : IOsuDataSource, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequestAt = DateTimeOffset.MinValue;

    public async Task<OsuUser> GetUserAsync(UserReference user, CancellationToken cancellationToken)
    {
        var key = user.ApiKey.StartsWith('@') ? "@" + Uri.EscapeDataString(user.ApiKey[1..]) : Uri.EscapeDataString(user.ApiKey);
        var path = $"api/v2/users/{key}";
        if (user.Mode is not null)
            path += "/" + user.Mode;
        return await GetAsync<OsuUser>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OsuScore>> GetScoresAsync(long userId, ScoreType type, string mode, int limit, int offset, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/v2/users/{userId}/scores/{HarvestSource.From(type)}?mode={mode}&limit={limit}&offset={offset}");
        return await GetAsync<List<OsuScore>>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OsuBeatmapPlaycount>> GetMostPlayedAsync(long userId, int limit, int offset, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture,
            $"api/v2/users/{userId}/beatmapsets/most_played?limit={limit}&offset={offset}");
        return await GetAsync<List<OsuBeatmapPlaycount>>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OsuBeatmapsetDetails> GetBeatmapsetAsync(long beatmapsetId, CancellationToken cancellationToken)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"api/v2/beatmapsets/{beatmapsetId}");
        return await GetAsync<OsuBeatmapsetDetails>(path, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        var uri = new Uri(options.BaseAddress, relativePath);
        var refreshedToken = false;

        for (var attempt = 0; ; attempt++)
        {
            var token = await tokens.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            using var response = await SendThrottledAsync(uri, token, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt >= options.MaxRetries)
                    throw new OsuApiException($"Rate limited by osu! after {attempt + 1} attempts.", response.StatusCode);
                var wait = RetryAfterHeader.Get(response.Headers, time.GetUtcNow()) ?? options.RetryBaseDelay * (attempt + 1);
                await Task.Delay(wait, time, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken)
            {
                tokens.Invalidate();
                refreshedToken = true;
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new OsuAuthenticationException("osu! rejected the access token. Check your credentials in Settings.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new OsuApiException($"Not found: {relativePath}", response.StatusCode);
            if (!response.IsSuccessStatusCode)
                throw new OsuApiException($"HTTP {(int)response.StatusCode} for {relativePath}", response.StatusCode);

            return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Api, cancellationToken).ConfigureAwait(false)
                ?? throw new OsuApiException($"Empty response for {relativePath}");
        }
    }

    private async Task<HttpResponseMessage> SendThrottledAsync(Uri uri, string token, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wait = _nextRequestAt - time.GetUtcNow();
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, time, cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json");
            try
            {
                return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _nextRequestAt = time.GetUtcNow() + options.MinRequestInterval;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
