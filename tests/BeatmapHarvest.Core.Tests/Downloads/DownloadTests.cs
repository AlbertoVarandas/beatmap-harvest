using System.Net;
using System.Net.Http.Headers;
using BeatmapHarvest.Core.Downloads;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Tests.Downloads;

public class OszFileNameTests
{
    [Theory]
    [InlineData(400078, "400078 Artist - Title.osz", "400078 Artist - Title.osz")]
    [InlineData(400078, "Artist - Title.osz", "400078 Artist - Title.osz")]
    [InlineData(400078, "\"Artist: Title?\"", "400078 Artist_ Title_.osz")]
    [InlineData(400078, null, "400078.osz")]
    [InlineData(400078, "  ", "400078.osz")]
    [InlineData(400078, "12345 Other.osz", "400078 12345 Other.osz")]
    public void Always_starts_with_the_set_id(long id, string? suggested, string expected)
    {
        Assert.Equal(expected, OszFileName.Build(id, suggested));
    }
}

public sealed class BeatmapDownloaderTests : IDisposable
{
    private static readonly byte[] OszBytes = [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5];

    private static readonly MirrorDefinition[] TwoMirrors =
    [
        new("first", "https://first.test/d/{id}"),
        new("second", "https://second.test/d/{id}"),
    ];

    private readonly TempDirectory _dir = new();

    private static HttpResponseMessage Osz(string? fileName)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(OszBytes) };
        if (fileName is not null)
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = fileName };
        return response;
    }

    private static HttpResponseMessage Html() =>
        new(HttpStatusCode.OK) { Content = new StringContent("<html>error</html>") };

    [Fact]
    public async Task Falls_back_when_a_mirror_returns_something_that_is_not_an_osz()
    {
        using var handler = new FakeHttpHandler(req => req.RequestUri!.Host == "first.test" ? Html() : Osz("Artist - Title.osz"));
        using var http = new HttpClient(handler);
        var downloader = new BeatmapDownloader(http, () => TwoMirrors, TimeProvider.System);

        var result = await downloader.DownloadAsync(77, _dir.Path, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("second", result.Mirror);
        Assert.Equal(Path.Combine(_dir.Path, "77 Artist - Title.osz"), result.FilePath);
        Assert.Equal(OszBytes, await File.ReadAllBytesAsync(result.FilePath!));
        Assert.Empty(Directory.EnumerateFiles(_dir.Path, "*.part"));
        Assert.Equal(1, downloader.Stats["first"].Failures);
    }

    [Fact]
    public async Task Fails_when_every_mirror_fails()
    {
        using var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);
        var downloader = new BeatmapDownloader(http, () => TwoMirrors, TimeProvider.System);

        var result = await downloader.DownloadAsync(77, _dir.Path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.Errors.Count);
        Assert.Empty(Directory.EnumerateFiles(_dir.Path));
    }

    [Fact]
    public async Task Disables_a_mirror_after_consecutive_failures()
    {
        using var handler = new FakeHttpHandler(req => req.RequestUri!.Host == "first.test"
            ? new HttpResponseMessage(HttpStatusCode.Forbidden)
            : Osz(null));
        using var http = new HttpClient(handler);
        var downloader = new BeatmapDownloader(http, () => TwoMirrors, TimeProvider.System) { DisableAfterConsecutiveFailures = 2 };

        for (var id = 1; id <= 4; id++)
            await downloader.DownloadAsync(id, _dir.Path, CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count(r => r.RequestUri!.Host == "first.test"));
    }

    [Fact]
    public async Task Runner_skips_done_and_existing_sets_and_records_new_ones()
    {
        using var handler = new FakeHttpHandler(_ => Osz(null));
        using var http = new HttpClient(handler);
        var downloader = new BeatmapDownloader(http, () => TwoMirrors, TimeProvider.System);
        var ledger = new DownloadLedger(_dir.File("done.json"));
        ledger.MarkDone(1);
        var downloads = _dir.File("downloads");
        Directory.CreateDirectory(downloads);
        await File.WriteAllTextAsync(Path.Combine(downloads, "2 Existing.osz"), "");
        await File.WriteAllTextAsync(Path.Combine(downloads, "3.osz.part"), "stale");

        var report = await new DownloadRunner(downloader, ledger)
            .RunAsync([1, 2, 3, 3], downloads, maxParallel: 2, progress: null, CancellationToken.None);

        Assert.Equal(1, report.Downloaded);
        Assert.Equal(2, report.Skipped);
        Assert.Empty(report.Failures);
        Assert.True(ledger.Contains(3));
        Assert.True(File.Exists(Path.Combine(downloads, "3.osz")));
        Assert.False(File.Exists(Path.Combine(downloads, "3.osz.part")));
    }

    public void Dispose() => _dir.Dispose();
}
