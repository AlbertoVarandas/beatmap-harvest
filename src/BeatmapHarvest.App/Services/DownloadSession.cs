using BeatmapHarvest.Core.Downloads;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

public enum DownloadItemState
{
    Queued,
    Downloaded,
    Skipped,
    Failed,
}

/// <summary>One beatmapset in the current download run. Updated from worker threads; read by the page.</summary>
public sealed class DownloadItem(long setId, string artist, string title)
{
    public long SetId { get; } = setId;
    public string Artist { get; } = artist;
    public string Title { get; } = title;
    public DownloadItemState State { get; internal set; }

    /// <summary>File name when downloaded, mirror errors when failed.</summary>
    public string? Detail { get; internal set; }
}

/// <summary>
/// App-wide download state, like <see cref="HarvestSession"/>: a running download survives navigation.
/// Components subscribe to <see cref="Changed"/> and re-render.
/// </summary>
public sealed class DownloadSession(DownloadRunner runner, BeatmapDownloader downloader, SettingsStore settings, AppPaths paths) : IDisposable
{
    private readonly Lock _lock = new();
    private Dictionary<long, DownloadItem> _byId = [];
    private CancellationTokenSource? _cts;

    public event Action? Changed;

    /// <summary>An array so it can feed &lt;Virtualize&gt; directly.</summary>
    public DownloadItem[] Items { get; private set; } = [];
    public bool IsRunning { get; private set; }
    public int Completed { get; private set; }
    public DownloadReport? Report { get; private set; }
    public string? Error { get; private set; }

    /// <summary>The folder of the current (or last) run.</summary>
    public string? Destination { get; private set; }

    public IReadOnlyDictionary<string, MirrorStats> MirrorStats => downloader.Stats;

    /// <summary>A mirror with this many failures in a row is skipped until the app restarts.</summary>
    public int DisableMirrorAfter => downloader.DisableAfterConsecutiveFailures;

    public int Count(DownloadItemState state)
    {
        lock (_lock)
            return Items.Count(i => i.State == state);
    }

    public string DownloadsDirectory => settings.Load().DownloadsDirectory ?? paths.DefaultDownloadsDirectory;
    public string SongsDirectory => settings.Load().SongsDirectory ?? AppPaths.DefaultSongsDirectory;

    /// <summary>Target of "Move downloads"; the Songs folder unless another one is set in Settings.</summary>
    public string MoveToDirectory => settings.Load().MoveToDirectory ?? SongsDirectory;

    /// <summary>Starts a run in the background; does nothing while one is running.</summary>
    public void Start(IEnumerable<MapsetInfo> mapsets)
    {
        if (IsRunning)
            return;

        var items = mapsets.DistinctBy(m => m.Id).Select(m => new DownloadItem(m.Id, m.Artist, m.Title)).ToArray();
        var current = settings.Load();
        var destination = current.DownloadsDirectory ?? paths.DefaultDownloadsDirectory;

        lock (_lock)
        {
            Items = items;
            _byId = items.ToDictionary(i => i.SetId);
        }
        _cts = new CancellationTokenSource();
        IsRunning = true;
        Completed = 0;
        Report = null;
        Error = null;
        Destination = destination;
        Changed?.Invoke();

        _ = RunAsync(items.Select(i => i.SetId).ToList(), destination, current.MaxParallelDownloads, _cts.Token);
    }

    /// <summary>Runs again with the sets that failed last time.</summary>
    public void RetryFailed()
    {
        List<DownloadItem> failed;
        lock (_lock)
            failed = Items.Where(i => i.State == DownloadItemState.Failed).ToList();
        if (failed.Count > 0)
            Start(failed.Select(i => new MapsetInfo { Id = i.SetId, Artist = i.Artist, Title = i.Title }));
    }

    public void Cancel() => _cts?.Cancel();

    /// <summary>Moves every .osz in the downloads folder to <see cref="MoveToDirectory"/> (osu! imports them from Songs on its next start).</summary>
    public Task<SongsFolder.MoveResult> MoveToSongsAsync()
    {
        var from = DownloadsDirectory;
        var to = MoveToDirectory;
        return Task.Run(() => SongsFolder.MoveArchives(from, to));
    }

    private async Task RunAsync(IReadOnlyCollection<long> ids, string destination, int maxParallel, CancellationToken cancellationToken)
    {
        try
        {
            Report = await Task.Run(
                () => runner.RunAsync(ids, destination, maxParallel, new Progress(this), cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Error = "Cancelled. Finished downloads are kept; start again to resume.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Disk error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            Changed?.Invoke();
        }
    }

    private void Apply(DownloadProgress value)
    {
        lock (_lock)
        {
            if (_byId.TryGetValue(value.SetId, out var item))
            {
                item.State = value.Status switch
                {
                    DownloadStatus.Downloaded => DownloadItemState.Downloaded,
                    DownloadStatus.Skipped => DownloadItemState.Skipped,
                    _ => DownloadItemState.Failed,
                };
                item.Detail = value.Detail;
            }
            Completed = Math.Max(Completed, value.Completed);
        }
        Changed?.Invoke();
    }

    public void Dispose() => _cts?.Dispose();

    // Not System.Progress<T>: that posts to the UI thread; the page marshals with InvokeAsync itself.
    private sealed class Progress(DownloadSession session) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => session.Apply(value);
    }
}
