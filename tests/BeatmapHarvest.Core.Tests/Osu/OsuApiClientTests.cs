using System.Net;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests.Osu;

public sealed class OsuApiClientTests : IDisposable
{
    private static readonly OsuApiOptions FastOptions = new()
    {
        MinRequestInterval = TimeSpan.Zero,
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        MaxRetries = 2,
    };

    private const string TokenJson = """{"token_type":"Bearer","expires_in":86400,"access_token":"tok-1"}""";

    private readonly List<IDisposable> _disposables = [];

    private OsuApiClient CreateClient(FakeHttpHandler handler, OsuCredentials? credentials = null)
    {
        var http = new HttpClient(handler);
        var tokens = new OsuTokenProvider(http, new InMemoryCredentialStore(credentials ?? new OsuCredentials(1, "secret")), FastOptions, TimeProvider.System);
        var client = new OsuApiClient(http, tokens, FastOptions, TimeProvider.System);
        _disposables.AddRange([http, tokens, client]);
        return client;
    }

    [Fact]
    public async Task Requests_a_token_once_and_sends_it_as_bearer()
    {
        var handler = new FakeHttpHandler(req => req.RequestUri!.AbsolutePath == "/oauth/token"
            ? FakeHttpHandler.Json(TokenJson)
            : FakeHttpHandler.Json("[]"));
        var client = CreateClient(handler);

        await client.GetMostPlayedAsync(7, 100, 0, CancellationToken.None);
        await client.GetMostPlayedAsync(7, 100, 100, CancellationToken.None);

        Assert.Single(handler.Requests, r => r.RequestUri!.AbsolutePath == "/oauth/token");
        var apiRequests = handler.Requests.Where(r => r.RequestUri!.AbsolutePath.StartsWith("/api/v2/", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, apiRequests.Count);
        Assert.All(apiRequests, r => Assert.Equal("Bearer tok-1", r.Headers.Authorization!.ToString()));
        Assert.Equal("/api/v2/users/7/beatmapsets/most_played?limit=100&offset=100", apiRequests[1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Builds_score_and_user_paths()
    {
        var handler = new FakeHttpHandler(req => req.RequestUri!.AbsolutePath switch
        {
            "/oauth/token" => FakeHttpHandler.Json(TokenJson),
            "/api/v2/users/@some%20name/mania" => FakeHttpHandler.Json("""{"id":42,"username":"some name","playmode":"mania"}"""),
            _ => FakeHttpHandler.Json("[]"),
        });
        var client = CreateClient(handler);

        var user = await client.GetUserAsync(new UserReference("@some name", "mania"), CancellationToken.None);
        await client.GetScoresAsync(42, ScoreType.Firsts, Ruleset.Taiko, 50, 10, CancellationToken.None);

        Assert.Equal(42, user.Id);
        Assert.Equal("mania", user.Playmode);
        Assert.Equal("/api/v2/users/42/scores/firsts?mode=taiko&limit=50&offset=10", handler.Requests[^1].RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Reads_beatmapset_details()
    {
        const string json = """
            {"id":318207,"artist":"Utsu-P","artist_unicode":"うつＰ","title":"Tokyo Teddy Bear","creator":"puxtu",
             "bpm":220,"play_count":1234567,"favourite_count":890,"ranked_date":"2015-06-01T10:00:00+00:00",
             "source":"","tags":"vocaloid rin","status":"ranked","video":false,"storyboard":true,
             "covers":{"cover@2x":"https://assets.ppy.sh/c.jpg"},
             "beatmaps":[{"id":736930,"version":"[4K] NM","mode":"mania","difficulty_rating":2.19,"total_length":187,
               "hit_length":180,"bpm":220,"cs":4,"ar":5,"accuracy":7,"drain":7.5,"count_circles":900,"count_sliders":120,
               "count_spinners":0,"max_combo":1500,"playcount":42,"passcount":30}]}
            """;
        var handler = new FakeHttpHandler(req => req.RequestUri!.AbsolutePath == "/oauth/token"
            ? FakeHttpHandler.Json(TokenJson)
            : FakeHttpHandler.Json(json));
        var client = CreateClient(handler);

        var set = await client.GetBeatmapsetAsync(318207, CancellationToken.None);

        Assert.Equal("/api/v2/beatmapsets/318207", handler.Requests[^1].RequestUri!.AbsolutePath);
        Assert.Equal("うつＰ", set.ArtistUnicode);
        Assert.Equal(220, set.Bpm);
        Assert.Equal(2015, set.RankedDate!.Value.Year);
        Assert.True(set.Storyboard);
        Assert.Equal("https://assets.ppy.sh/c.jpg", set.Covers!.Cover2x);
        var map = Assert.Single(set.Beatmaps);
        Assert.Equal(7, map.Accuracy);
        Assert.Equal(7.5, map.Drain);
        Assert.Equal(1500, map.MaxCombo);
        Assert.Equal(120, map.CountSliders);
    }

    [Fact]
    public async Task Retries_the_same_request_after_429()
    {
        var calls = 0;
        var handler = new FakeHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/oauth/token")
                return FakeHttpHandler.Json(TokenJson);
            return ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : FakeHttpHandler.Json("""[{"beatmap_id":1,"count":3}]""");
        });
        var client = CreateClient(handler);

        var page = await client.GetMostPlayedAsync(7, 100, 0, CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(3, Assert.Single(page).Count);
    }

    [Fact]
    public async Task Gives_up_after_max_retries()
    {
        var handler = new FakeHttpHandler(req => req.RequestUri!.AbsolutePath == "/oauth/token"
            ? FakeHttpHandler.Json(TokenJson)
            : new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<OsuApiException>(() => client.GetMostPlayedAsync(7, 100, 0, CancellationToken.None));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task Refreshes_the_token_once_after_401()
    {
        var tokenCalls = 0;
        var handler = new FakeHttpHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/oauth/token")
            {
                tokenCalls++;
                return FakeHttpHandler.Json($$"""{"token_type":"Bearer","expires_in":86400,"access_token":"tok-{{tokenCalls}}"}""");
            }
            return req.Headers.Authorization!.Parameter == "tok-1"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : FakeHttpHandler.Json("[]");
        });
        var client = CreateClient(handler);

        await client.GetMostPlayedAsync(7, 100, 0, CancellationToken.None);

        Assert.Equal(2, tokenCalls);
    }

    [Fact]
    public async Task Rejected_credentials_raise_an_authentication_error()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"error":"invalid_client"}""", HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<OsuAuthenticationException>(() => client.GetMostPlayedAsync(7, 100, 0, CancellationToken.None));
    }

    [Fact]
    public void Credentials_never_print_the_secret()
    {
        Assert.DoesNotContain("hunter2", new OsuCredentials(5, "hunter2").ToString(), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var d in _disposables)
            d.Dispose();
    }
}
