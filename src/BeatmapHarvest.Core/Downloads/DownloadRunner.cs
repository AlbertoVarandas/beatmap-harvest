using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Downloads;

public enum DownloadStatus
{
    Skipped,
    Downloaded,
    Failed,
}

public sealed record DownloadProgress(long SetId, DownloadStatus Status, int Completed, int Total, string? Detail);

public sealed record DownloadReport(int Downloaded, int Skipped, IReadOnlyList<DownloadResult> Failures);

/// <summary>Downloads a queue of beatmapsets in parallel, skipping what is already done and resuming after failures.</summary>
public sealed class DownloadRunner(BeatmapDownloader downloader, DownloadLedger ledger)
{
    public async Task<DownloadReport> RunAsync(
        IReadOnlyCollection<long> setIds,
        string destinationDirectory,
        int maxParallel,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        RemoveStalePartFiles(destinationDirectory);

        var onDisk = OwnedMapsetScanner.Scan(destinationDirectory, includeDirectories: false);
        var total = setIds.Count;
        var completed = 0;
        var downloaded = 0;
        var skipped = 0;
        var failures = new List<DownloadResult>();
        var failuresLock = new Lock();

        void Report(long id, DownloadStatus status, string? detail) =>
            progress?.Report(new DownloadProgress(id, status, Interlocked.Increment(ref completed), total, detail));

        await Parallel.ForEachAsync(
            setIds.Distinct(),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, maxParallel), CancellationToken = cancellationToken },
            async (id, ct) =>
            {
                if (ledger.Contains(id) || onDisk.Contains(id))
                {
                    Interlocked.Increment(ref skipped);
                    Report(id, DownloadStatus.Skipped, null);
                    return;
                }

                var result = await downloader.DownloadAsync(id, destinationDirectory, ct).ConfigureAwait(false);
                if (result.Success)
                {
                    ledger.MarkDone(id);
                    Interlocked.Increment(ref downloaded);
                    Report(id, DownloadStatus.Downloaded, Path.GetFileName(result.FilePath));
                }
                else
                {
                    lock (failuresLock)
                        failures.Add(result);
                    Report(id, DownloadStatus.Failed, string.Join("; ", result.Errors));
                }
            }).ConfigureAwait(false);

        return new DownloadReport(downloaded, skipped, failures);
    }

    private static void RemoveStalePartFiles(string directory)
    {
        foreach (var part in Directory.EnumerateFiles(directory, "*.part"))
        {
            try
            {
                File.Delete(part);
            }
            catch (IOException)
            {
                // Still in use; it will be overwritten when that set is downloaded again.
            }
        }
    }
}
