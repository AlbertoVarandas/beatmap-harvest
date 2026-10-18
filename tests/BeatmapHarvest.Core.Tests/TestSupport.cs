using System.Net;
using System.Text;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests;

/// <summary>Answers HTTP requests from a delegate and records them.</summary>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

internal sealed class InMemoryCredentialStore(OsuCredentials? credentials = null) : ICredentialStore
{
    private OsuCredentials? _credentials = credentials;
    public OsuCredentials? Load() => _credentials;
    public void Save(OsuCredentials credentials) => _credentials = credentials;
    public void Clear() => _credentials = null;
}

/// <summary>A temporary directory deleted at the end of the test.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("beatmap-harvest-tests-").FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class Samples
{
    public static OsuBeatmapset Set(long id, string artist = "Artist", string title = "Title") => new()
    {
        Id = id,
        Artist = artist,
        Title = title,
        Creator = "Mapper",
        Status = "ranked",
        PreviewUrl = $"//b.ppy.sh/preview/{id}.mp3",
        Covers = new OsuCovers { List2x = $"https://assets.ppy.sh/beatmaps/{id}/covers/list@2x.jpg" },
    };

    public static OsuBeatmap Map(long id, long setId, string mode = Ruleset.Mania, double stars = 2.5, int length = 120) => new()
    {
        Id = id,
        BeatmapsetId = setId,
        Version = $"Diff {id}",
        Mode = mode,
        DifficultyRating = stars,
        TotalLength = length,
        Status = "ranked",
    };

    public static MapsetInfo Mapset(long id, params DifficultyInfo[] difficulties) => new()
    {
        Id = id,
        Artist = "Artist",
        Title = $"Song {id}",
        Creator = "Mapper",
        Status = "ranked",
        Sources = [HarvestSource.MostPlayed],
        Difficulties = difficulties,
    };

    public static DifficultyInfo Diff(string mode = Ruleset.Mania, double stars = 3, int length = 120, int? plays = 1) => new()
    {
        Id = Random.Shared.NextInt64(1, long.MaxValue),
        Version = "Normal",
        Mode = mode,
        Stars = stars,
        LengthSeconds = length,
        Plays = plays,
    };
}
