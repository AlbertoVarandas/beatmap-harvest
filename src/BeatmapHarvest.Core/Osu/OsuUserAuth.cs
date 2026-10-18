using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BeatmapHarvest.Core.Json;

namespace BeatmapHarvest.Core.Osu;

/// <summary>A token that acts as the signed-in user. Never printed: <see cref="ToString"/> hides it.</summary>
public sealed record OsuUserToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public override string ToString() => $"OsuUserToken {{ ExpiresAt = {ExpiresAt:O} }}";
}

/// <summary>
/// "Sign in with osu!" through the OAuth authorization-code grant: the user approves the app on osu.ppy.sh,
/// osu! redirects to a local address with a one-time code, and the code is exchanged for a token.
/// The app never sees the user's password.
/// </summary>
public static class OsuUserAuth
{
    /// <summary>Own profile, public data, and the friend list.</summary>
    public const string Scopes = "identify public friends.read";

    public static Uri AuthorizeUri(Uri baseAddress, int clientId, Uri redirectUri, string state)
    {
        var query = string.Join('&',
            "client_id=" + clientId.ToString(CultureInfo.InvariantCulture),
            "redirect_uri=" + Uri.EscapeDataString(redirectUri.ToString()),
            "response_type=code",
            "scope=" + Uri.EscapeDataString(Scopes),
            "state=" + Uri.EscapeDataString(state));
        return new Uri(baseAddress, "oauth/authorize?" + query);
    }

    public static Task<OsuUserToken> ExchangeCodeAsync(
        HttpClient http, Uri baseAddress, OsuCredentials credentials, string code, Uri redirectUri, TimeProvider time, CancellationToken cancellationToken) =>
        RequestAsync(http, baseAddress, credentials, time, cancellationToken,
            ("grant_type", "authorization_code"), ("code", code), ("redirect_uri", redirectUri.ToString()));

    public static Task<OsuUserToken> RefreshAsync(
        HttpClient http, Uri baseAddress, OsuCredentials credentials, string refreshToken, TimeProvider time, CancellationToken cancellationToken) =>
        RequestAsync(http, baseAddress, credentials, time, cancellationToken,
            ("grant_type", "refresh_token"), ("refresh_token", refreshToken));

    public static async Task<OsuUser> GetMeAsync(HttpClient http, Uri baseAddress, string accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(http, HttpMethod.Get, new Uri(baseAddress, "api/v2/me"), accessToken, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<OsuUser>(JsonDefaults.Api, cancellationToken).ConfigureAwait(false)
            ?? throw new OsuApiException("Empty response for /me");
    }

    /// <summary>The signed-in user's friends. Accepts both response shapes the API has used (users, or relations with a <c>target</c> user).</summary>
    public static async Task<IReadOnlyList<OsuUser>> GetFriendsAsync(HttpClient http, Uri baseAddress, string accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(http, HttpMethod.Get, new Uri(baseAddress, "api/v2/friends"), accessToken, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseFriends(document.RootElement);
    }

    /// <summary>Invalidates the token on osu!'s side (sign out).</summary>
    public static async Task RevokeAsync(HttpClient http, Uri baseAddress, string accessToken, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(http, HttpMethod.Delete, new Uri(baseAddress, "api/v2/oauth/tokens/current"), accessToken, cancellationToken, throwOnError: false).ConfigureAwait(false);
    }

    internal static IReadOnlyList<OsuUser> ParseFriends(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return [];
        var friends = new List<OsuUser>();
        foreach (var item in root.EnumerateArray())
        {
            var user = item.TryGetProperty("target", out var target) && target.ValueKind == JsonValueKind.Object ? target : item;
            if (user.Deserialize<OsuUser>(JsonDefaults.Api) is { Id: > 0 } friend)
                friends.Add(friend);
        }
        return friends.OrderBy(f => f.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<OsuUserToken> RequestAsync(
        HttpClient http, Uri baseAddress, OsuCredentials credentials, TimeProvider time, CancellationToken cancellationToken,
        params (string Key, string Value)[] grant)
    {
        var fields = new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId.ToString(CultureInfo.InvariantCulture),
            ["client_secret"] = credentials.ClientSecret,
        };
        foreach (var (key, value) in grant)
            fields[key] = value;

        using var content = new FormUrlEncodedContent(fields);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, "oauth/token")) { Content = content };
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            throw new OsuAuthenticationException("osu! did not accept the sign-in. Sign in again.");
        if (!response.IsSuccessStatusCode)
            throw new OsuApiException($"Sign-in failed with HTTP {(int)response.StatusCode}.", response.StatusCode);

        var token = await response.Content.ReadFromJsonAsync<OsuTokenResponse>(JsonDefaults.Api, cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrEmpty(token.AccessToken) || string.IsNullOrEmpty(token.RefreshToken))
            throw new OsuApiException("osu! returned no user token.");
        return new OsuUserToken(token.AccessToken, token.RefreshToken, time.GetUtcNow() + TimeSpan.FromSeconds(token.ExpiresIn) - TimeSpan.FromMinutes(5));
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient http, HttpMethod method, Uri uri, string accessToken, CancellationToken cancellationToken, bool throwOnError = true)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!throwOnError || response.IsSuccessStatusCode)
            return response;

        var status = response.StatusCode;
        response.Dispose();
        throw status == HttpStatusCode.Unauthorized
            ? new OsuAuthenticationException("The osu! sign-in has expired. Sign in again.")
            : new OsuApiException($"HTTP {(int)status} for {uri.AbsolutePath}", status);
    }
}
