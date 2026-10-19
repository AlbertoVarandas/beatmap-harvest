using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// App-wide harvest state. Lives outside the pages so a running harvest survives navigation.
/// Components subscribe to <see cref="Changed"/> and re-render.
/// </summary>
public sealed class HarvestSession(ProfileHarvester harvester, ProfileLibrary profiles, Localizer l) : IDisposable
{
    private const int MaxLogLines = 300;

    private Localizer Text => l;

    private readonly Lock _lock = new();
    private readonly List<string> _log = [];
    private CancellationTokenSource? _cts;

    public event Action? Changed;

    /// <summary>Raised after a harvest finished and was saved, with the new result.</summary>
    public event Action<HarvestResult>? Completed;

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
            profiles.Put(result);
            finished = result;
        }
        catch (OperationCanceledException)
        {
            Error = l["Cancelled."];
        }
        catch (OsuApiException ex)
        {
            Error = l[ex.Message];
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            Error = l.F("Network or disk error: {0}", ex.Message);
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
            var message = session.Text.F(value.Template, value.Args);
            session.Fraction = value.Fraction;
            session.Status = message;
            session.Append(message);
        }
    }
}
