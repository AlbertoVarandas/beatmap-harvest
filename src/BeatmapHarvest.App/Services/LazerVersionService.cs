using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BeatmapHarvest.App.Services;

/// <summary>The newest osu!lazer release (the tag of ppy/osu's latest GitHub release), fetched once per run.</summary>
public sealed class LazerVersionService(HttpClient http)
{
    private const string LatestRelease = "https://api.github.com/repos/ppy/osu/releases/latest";

    private readonly Lock _lock = new();
    private Task<string?>? _latest;

    /// <summary>e.g. <c>2026.1018.0</c>; null when GitHub can't be reached.</summary>
    public Task<string?> LatestAsync()
    {
        lock (_lock)
            return _latest ??= FetchAsync();
    }

    private async Task<string?> FetchAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var release = await http.GetFromJsonAsync<Release>(LatestRelease, timeout.Token).ConfigureAwait(false);
            return release?.TagName;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            lock (_lock)
                _latest = null; // try again next time the page opens
            return null;
        }
    }

    private sealed record Release([property: JsonPropertyName("tag_name")] string? TagName);
}
