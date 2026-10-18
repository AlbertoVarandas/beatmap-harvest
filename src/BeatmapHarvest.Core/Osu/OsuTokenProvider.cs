using System.Net;
using System.Net.Http.Json;
using BeatmapHarvest.Core.Json;

namespace BeatmapHarvest.Core.Osu;

public interface IAccessTokenProvider
{
    ValueTask<string> GetTokenAsync(CancellationToken cancellationToken);

    /// <summary>Forget the cached token (after a 401 or when the credentials change).</summary>
    void Invalidate();
}

/// <summary>
/// Gets and caches an app token through the OAuth client-credentials grant (scope <c>public</c>).
/// No user login is involved; the token reads public profile data only.
/// </summary>
public sealed class OsuTokenProvider(HttpClient http, ICredentialStore credentials, OsuApiOptions options, TimeProvider time)
    : IAccessTokenProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null && time.GetUtcNow() < _expiresAt)
                return _token;

            var creds = credentials.Load()
                ?? throw new OsuAuthenticationException("No osu! API credentials configured. Add your client id and secret in Settings.");

            var response = await RequestTokenAsync(http, creds, options.BaseAddress, cancellationToken).ConfigureAwait(false);
            _token = response.AccessToken;
            _expiresAt = time.GetUtcNow() + TimeSpan.FromSeconds(response.ExpiresIn) - options.TokenExpiryMargin;
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Invalidate()
    {
        _lock.Wait();
        try
        {
            _token = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Requests a token for the given credentials without caching it. Used by "Test connection".</summary>
    public static async Task<TimeSpan> VerifyAsync(HttpClient http, OsuCredentials credentials, Uri baseAddress, CancellationToken cancellationToken)
    {
        var response = await RequestTokenAsync(http, credentials, baseAddress, cancellationToken).ConfigureAwait(false);
        return TimeSpan.FromSeconds(response.ExpiresIn);
    }

    private static async Task<OsuTokenResponse> RequestTokenAsync(HttpClient http, OsuCredentials creds, Uri baseAddress, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = creds.ClientId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["client_secret"] = creds.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = "public",
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "oauth/token")) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new OsuAuthenticationException("osu! rejected the client id or secret. Check them in Settings.");
        if (!response.IsSuccessStatusCode)
            throw new OsuApiException($"Token request failed with HTTP {(int)response.StatusCode}.", response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<OsuTokenResponse>(JsonDefaults.Api, cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            throw new OsuApiException("Token response did not contain an access token.");
        return token;
    }

    public void Dispose() => _lock.Dispose();
}
