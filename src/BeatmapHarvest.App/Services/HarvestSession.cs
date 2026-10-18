using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// App-wide harvest state. Lives outside the pages so a running harvest survives navigation.
/// Components subscribe to <see cref="Changed"/> and re-render.
/// </summary>
public sealed class HarvestSession(ProfileHarvester harvester, ProfileLibrary profiles) : IDisposable
{
    private const int MaxLogLines = 300;

    private readonly Lock _lock = new();
    private readonly List<string> _log = [];
    private CancellationTokenSource? _cts;
    private bool _loaded;

    public event Action? Changed;

    /// <summary>Raised after a harvest finished and was saved, with the new result.</summary>
    public event Action<HarvestResult>? Completed;

    public HarvestResult? Current { get; private set; }
    public HarvestSummary? Summary { get; private set; }
    public bool IsRunning { get; private set; }
    public string? Error { get; private set; }

    /// <summary>Estimated progress of the running harvest, 0 to 1.</summary>
    public double Fraction { get; private set; }

    /// <summary>The latest progress message.</summary>
    public string? Status { get; private set; }

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
        if (profiles.Latest is { } latest)
            SetResult(latest);
    }

    /// <summary>Called when a profile is removed, so the Harvest page stops showing it.</summary>
    public void Forget(long userId)
    {
        if (Current?.UserId != userId)
            return;
        Current = null;
        Summary = null;
        if (profiles.Latest is { } latest)
            SetResult(latest);
        Changed?.Invoke();
    }

    /// <summary>Starts a harvest in the background (does nothing while one is running).</summary>
    public void Start(UserReference profile, HarvestOptions? options = null) => _ = StartAsync(profile, options ?? new HarvestOptions());

    public async Task StartAsync(UserReference profile, HarvestOptions options)
    {
        if (IsRunning)
            return;
        HarvestResult? finished = null;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        Error = null;
        Fraction = 0;
        Status = null;
        lock (_lock)
            _log.Clear();
        Changed?.Invoke();

        try
        {
            var progress = new Progress(this);
            var result = await Task.Run(() => harvester.HarvestAsync(profile, options, progress, _cts.Token)).ConfigureAwait(false);
            SetResult(result);
            profiles.Put(result);
            finished = result;
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
        if (finished is not null)
            Completed?.Invoke(finished);
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
        public void Report(HarvestProgress value)
        {
            session.Fraction = value.Fraction;
            session.Status = value.Message;
            session.Append(value.Message);
        }
    }
}
