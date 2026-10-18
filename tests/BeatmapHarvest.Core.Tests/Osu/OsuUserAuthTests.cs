using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests.Osu;

public class OsuUserAuthTests
{
    private static readonly Uri Base = new("https://osu.ppy.sh/");

    [Fact]
    public void Builds_the_authorize_url()
    {
        var uri = OsuUserAuth.AuthorizeUri(Base, 71715, new Uri("http://localhost:7270/callback"), "st@te");

        Assert.Equal(
            "https://osu.ppy.sh/oauth/authorize?client_id=71715&redirect_uri=http%3A%2F%2Flocalhost%3A7270%2Fcallback"
            + "&response_type=code&scope=identify%20public%20friends.read&state=st%40te",
            uri.AbsoluteUri);
    }

    [Fact]
    public async Task Exchanges_the_code_for_a_user_token()
    {
        string? body = null;
        using var handler = new FakeHttpHandler(req =>
        {
            body = req.Content!.ReadAsStringAsync().Result;
            return FakeHttpHandler.Json("""{"token_type":"Bearer","expires_in":86400,"access_token":"user-at","refresh_token":"user-rt"}""");
        });
        using var http = new HttpClient(handler);

        var token = await OsuUserAuth.ExchangeCodeAsync(
            http, Base, new OsuCredentials(5, "sec"), "the-code", new Uri("http://localhost:7270/callback"), TimeProvider.System, CancellationToken.None);

        Assert.Equal("user-at", token.AccessToken);
        Assert.Equal("user-rt", token.RefreshToken);
        Assert.False(token.IsExpired(DateTimeOffset.UtcNow));
        Assert.Contains("grant_type=authorization_code", body, StringComparison.Ordinal);
        Assert.Contains("code=the-code", body, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A7270%2Fcallback", body, StringComparison.Ordinal);
        Assert.DoesNotContain("user-at", token.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejected_code_is_an_authentication_error()
    {
        using var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest));
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<OsuAuthenticationException>(() => OsuUserAuth.ExchangeCodeAsync(
            http, Base, new OsuCredentials(5, "sec"), "bad", new Uri("http://localhost:7270/callback"), TimeProvider.System, CancellationToken.None));
    }

    [Theory]
    [InlineData("""[{"id":2,"username":"peppy","avatar_url":"a"},{"id":3,"username":"Ameo"}]""")]
    [InlineData("""[{"target_id":2,"relation_type":"friend","target":{"id":2,"username":"peppy","avatar_url":"a"}},{"target":{"id":3,"username":"Ameo"}}]""")]
    public void Reads_both_friend_list_shapes(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var friends = OsuUserAuth.ParseFriends(doc.RootElement);

        Assert.Equal(["Ameo", "peppy"], friends.Select(f => f.Username));
        Assert.Equal("a", friends[1].AvatarUrl);
    }

    [Fact]
    public async Task Loopback_listener_returns_the_callback_query()
    {
        var port = FreePort();
        using var listener = new LoopbackCallbackListener(port);
        var wait = listener.WaitAsync(CancellationToken.None);

        using var http = new HttpClient();
        using var favicon = await http.GetAsync(new Uri($"http://localhost:{port}/favicon.ico"));
        using var callback = await http.GetAsync(new Uri($"http://localhost:{port}/callback?code=abc&state=xyz"));
        var query = await wait;

        Assert.Equal(HttpStatusCode.NotFound, favicon.StatusCode);
        Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
        Assert.Contains("Signed in", await callback.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("abc", query["code"]);
        Assert.Equal("xyz", query["state"]);
        Assert.Equal(new Uri($"http://localhost:{port}/callback"), listener.RedirectUri);
    }

    [Fact]
    public async Task Loopback_listener_can_be_cancelled()
    {
        using var listener = new LoopbackCallbackListener(FreePort());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listener.WaitAsync(cts.Token));
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
