using System.ComponentModel;
using System.Diagnostics;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.App.Services;

/// <summary>Finds running osu! clients. Both are called "osu!", so they are told apart by where the executable lives.</summary>
public static class OsuProcesses
{
    public static bool IsRunning(OsuClient client)
    {
        var (stable, lazer) = Running();
        return client == OsuClient.Lazer ? lazer : stable;
    }

    public static (bool Stable, bool Lazer) Running()
    {
        var processes = Process.GetProcessesByName("osu!");
        var stable = false;
        var lazer = false;
        try
        {
            foreach (var process in processes)
            {
                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Can't inspect it (exited, or elevated): count it for both, to stay on the safe side.
                    return (true, true);
                }
                if (path?.Contains("osulazer", StringComparison.OrdinalIgnoreCase) == true)
                    lazer = true;
                else
                    stable = true;
            }
            return (stable, lazer);
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }
}

/// <summary>
/// Keeps track of which osu! clients are open (checked every two seconds), so pages can lock and unlock the actions
/// that need a client closed as soon as it opens or exits.
/// </summary>
public sealed class OsuProcessWatcher : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private readonly Lock _lock = new();
    private readonly Timer _timer;

    public OsuProcessWatcher()
    {
        Check();
        _timer = new Timer(_ => Check(), null, Interval, Interval);
    }

    public event Action? Changed;

    public bool StableRunning { get; private set; }
    public bool LazerRunning { get; private set; }

    public bool IsRunning(OsuClient client) => client == OsuClient.Lazer ? LazerRunning : StableRunning;

    /// <summary>Checks now (right before an action that needs a client closed) and returns the fresh state.</summary>
    public (bool Stable, bool Lazer) Check()
    {
        var running = OsuProcesses.Running();
        bool changed;
        lock (_lock)
        {
            changed = running.Stable != StableRunning || running.Lazer != LazerRunning;
            StableRunning = running.Stable;
            LazerRunning = running.Lazer;
        }
        if (changed)
            Changed?.Invoke();
        return running;
    }

    public void Dispose() => _timer.Dispose();
}
