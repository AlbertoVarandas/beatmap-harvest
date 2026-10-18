using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// App-wide harvest state. Lives outside the pages so a running harvest survives navigation.
/// Components subscribe to <see cref="Changed"/> and re-render.
/// </summary>
public sealed class HarvestSession(ProfileHarvester harvester, HarvestStore store) : IDisposable
{
    private const int MaxLogLines = 300;

    private readonly Lock _lock = new();
    private readonly List<string> _log = [];
    private CancellationTokenSource? _cts;
    private bool _loaded;

    public event Action? Changed;

    public HarvestResult? Current { get; private set; }
    public HarvestSummary? Summary { get; private set; }
    public bool IsRunning { get; private set; }
    public string? Error { get; private set; }

    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_lock)
                return [.. _log];
        }
    }

    /// <summary>Shows the most recent saved harvest when the app opens.</summary>
    public void LoadLatest()
    {
        if (_loaded || IsRunning)
            return;
        _loaded = true;
        var saved = store.LoadAll();
        if (saved.Count > 0)
            SetResult(saved[0]);
    }

    public async Task StartAsync(UserReference profile, HarvestOptions options)
    {
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        Error = null;
        lock (_lock)
            _log.Clear();
        Changed?.Invoke();

        try
        {
            var progress = new Progress(this);
            var result = await Task.Run(() => harvester.HarvestAsync(profile, options, progress, _cts.Token)).ConfigureAwait(false);
            store.Save(result);
            SetResult(result);
        }
        catch (OperationCanceledException)
        {
            Error = "Cancelled.";
        }
        catch (OsuApiException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Error = $"Network or disk error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
            Changed?.Invoke();
        }
    }

    public void Cancel() => _cts?.Cancel();

    private void SetResult(HarvestResult result)
    {
        Current = result;
        Summary = HarvestSummary.From(result.Mapsets);
    }

    private void Append(string line)
    {
        lock (_lock)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines)
                _log.RemoveAt(0);
        }
        Changed?.Invoke();
    }

    public void Dispose() => _cts?.Dispose();

    private sealed class Progress(HarvestSession session) : IProgress<HarvestProgress>
    {
        public void Report(HarvestProgress value) => session.Append(value.Message);
    }
}
