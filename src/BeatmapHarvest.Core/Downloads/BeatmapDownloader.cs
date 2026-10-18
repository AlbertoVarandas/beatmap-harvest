using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using BeatmapHarvest.Core.Http;

namespace BeatmapHarvest.Core.Downloads;

public sealed record DownloadResult(long SetId, bool Success, string? FilePath, string? Mirror, IReadOnlyList<string> Errors);

public sealed record MirrorStats(int Successes, int Failures, int ConsecutiveFailures);

/// <summary>
/// Downloads one beatmapset from the first mirror that delivers a valid archive.
/// Writes to <c>.part</c> and renames when complete; rejects anything that is not a ZIP (an .osz is a ZIP).
/// </summary>
public sealed class BeatmapDownloader(HttpClient http, Func<IReadOnlyList<MirrorDefinition>> mirrors, TimeProvider time)
{
    private static readonly byte[] ZipHeader = "PK\u0003\u0004"u8.ToArray();

    /// <summary>A mirror that fails this many times in a row is skipped for the rest of the session.</summary>
    public int DisableAfterConsecutiveFailures { get; init; } = 5;

    /// <summary>Wait after a 429 without Retry-After.</summary>
    public TimeSpan DefaultCooldown { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Wait for a cooling-down mirror only if it frees up within this time; otherwise try the next one.</summary>
    public TimeSpan MaxCooldownWait { get; init; } = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, MirrorStats> _stats = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldownUntil = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, MirrorStats> Stats => _stats;

    public async Task<DownloadResult> DownloadAsync(long setId, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        var errors = new List<string>();

        foreach (var mirror in mirrors().Where(m => m.Enabled))
        {
            if (IsDisabled(mirror))
                continue;

            var uri = mirror.BuildUri(setId);
            if (!await WaitForCooldownAsync(uri.Host, cancellationToken).ConfigureAwait(false))
            {
                errors.Add($"{mirror.Name}: rate limited");
                continue;
            }

            try
            {
                var path = await TryMirrorAsync(setId, uri, destinationDirectory, cancellationToken).ConfigureAwait(false);
                Record(mirror, success: true);
                return new DownloadResult(setId, true, path, mirror.Name, errors);
            }
            catch (MirrorException ex)
            {
                errors.Add($"{mirror.Name}: {ex.Message}");
                Record(mirror, success: false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                errors.Add($"{mirror.Name}: {ex.Message}");
                Record(mirror, success: false);
            }
        }

        return new DownloadResult(setId, false, null, null, errors);
    }

    private async Task<string> TryMirrorAsync(long setId, Uri uri, string destinationDirectory, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var now = time.GetUtcNow();
            _cooldownUntil[uri.Host] = now + (RetryAfterHeader.Get(response.Headers, now) ?? DefaultCooldown);
            throw new MirrorException("HTTP 429");
        }
        if (!response.IsSuccessStatusCode)
            throw new MirrorException($"HTTP {(int)response.StatusCode}");

        var fileName = OszFileName.Build(setId, SuggestedName(response.Content.Headers.ContentDisposition));
        var finalPath = Path.Combine(destinationDirectory, fileName);
        var partPath = finalPath + ".part";

        try
        {
            var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (source.ConfigureAwait(false))
            {
                // Check the first bytes before writing anything: error pages often come back as 200 + HTML.
                var header = new byte[ZipHeader.Length];
                var read = await source.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                if (read < header.Length || !header.AsSpan().SequenceEqual(ZipHeader))
                    throw new MirrorException("response is not an .osz archive");

                var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await using (target.ConfigureAwait(false))
                {
                    await target.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                    await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                    await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            File.Move(partPath, finalPath, overwrite: true);
            return finalPath;
        }
        catch
        {
            TryDelete(partPath);
            throw;
        }
    }

    private async Task<bool> WaitForCooldownAsync(string host, CancellationToken cancellationToken)
    {
        if (!_cooldownUntil.TryGetValue(host, out var until))
            return true;
        var wait = until - time.GetUtcNow();
        if (wait <= TimeSpan.Zero)
            return true;
        if (wait > MaxCooldownWait)
            return false;
        await Task.Delay(wait, time, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private bool IsDisabled(MirrorDefinition mirror) =>
        _stats.TryGetValue(mirror.Name, out var s) && s.ConsecutiveFailures >= DisableAfterConsecutiveFailures;

    private void Record(MirrorDefinition mirror, bool success) =>
        _stats.AddOrUpdate(
            mirror.Name,
            _ => success ? new MirrorStats(1, 0, 0) : new MirrorStats(0, 1, 1),
            (_, s) => success
                ? s with { Successes = s.Successes + 1, ConsecutiveFailures = 0 }
                : s with { Failures = s.Failures + 1, ConsecutiveFailures = s.ConsecutiveFailures + 1 });

    private static string? SuggestedName(ContentDispositionHeaderValue? disposition) =>
        disposition?.FileNameStar ?? disposition?.FileName;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Left-over .part files are removed at the start of the next run.
        }
    }

    private sealed class MirrorException(string message) : Exception(message);
}
