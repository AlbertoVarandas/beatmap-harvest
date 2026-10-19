using System.ComponentModel;
using System.Diagnostics;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Sends downloaded .osz files to osu!lazer in batches (<c>osu!.exe a.osz b.osz …</c>), so lazer imports them itself.
/// It only starts while lazer is closed: the first batch starts it and is imported on startup; the app waits for its
/// window before sending the next batches, which lazer receives through its IPC channel.
/// </summary>
public sealed class LazerImportService(OsuLibrary library, OsuProcessWatcher processes, Localizer l)
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Time for a just-opened lazer to start listening for imports after its window appears.</summary>
    private static readonly TimeSpan IpcWarmUp = TimeSpan.FromSeconds(8);

    public event Action? Changed;

    public bool IsRunning { get; private set; }
    public int Sent { get; private set; }
    public int Total { get; private set; }
    public string? Status { get; private set; }
    public string? Error { get; private set; }

    /// <summary>The downloaded .osz files that are not in osu!lazer yet.</summary>
    public async Task<IReadOnlyList<string>> PendingAsync()
    {
        var snapshot = await library.GetAsync().ConfigureAwait(false);
        return LazerImport.PendingArchives(library.DownloadsDirectory, snapshot.Ownership.InGame);
    }

    public async Task ImportAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        if (IsRunning || files.Count == 0)
            return;

        var exe = library.Clients.Lazer?.Executable;
        if (exe is null)
        {
            Error = l["osu!lazer's program was not found, so the maps can't be sent to it. Drag them onto its window instead."];
            Changed?.Invoke();
            return;
        }
        // Only started with osu!lazer closed: the first batch then opens it, and the rest follow while it starts up.
        if (processes.Check().Lazer)
        {
            Error = l["Close osu!lazer first. Beatmap Harvest then opens it with the maps."];
            Changed?.Invoke();
            return;
        }

        IsRunning = true;
        Sent = 0;
        Total = files.Count;
        Error = null;
        Changed?.Invoke();

        try
        {
            var batches = LazerImport.Batches(files);
            var lazerOpen = false;
            for (var i = 0; i < batches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = batches[i];
                Status = l.F("Sending batch {0} of {1} to osu!lazer…", i + 1, batches.Count);
                Changed?.Invoke();

                using var process = Start(exe, batch);
                if (!lazerOpen)
                {
                    // This process is the game itself: it imports these files once it has started.
                    Status = l["Waiting for osu!lazer to open…"];
                    Changed?.Invoke();
                    await WaitForWindowAsync(process, cancellationToken).ConfigureAwait(false);
                    lazerOpen = true;
                }
                else
                {
                    // A forwarding process: hands the paths to the running game (up to 3 s each) and exits.
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(batch.Count * 3 + 20));
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException(l["osu!lazer did not accept the maps. Make sure it is open and try again."]);
                }

                Sent += batch.Count;
                Changed?.Invoke();
            }
            Status = l.F("Sent {0} maps to osu!lazer. It imports them in the background; see its notifications.", Sent);
            library.Reload();
        }
        catch (OperationCanceledException)
        {
            Error = l["Stopped. Maps already sent are still imported by osu!lazer."];
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException)
        {
            Error = ex.Message;
        }
        finally
        {
            IsRunning = false;
            Changed?.Invoke();
        }
    }

    private static Process Start(string exe, IReadOnlyList<string> files)
    {
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
        };
        foreach (var file in files)
            info.ArgumentList.Add(file);
        return Process.Start(info) ?? throw new InvalidOperationException("osu!lazer could not be started.");
    }

    private static async Task WaitForWindowAsync(Process process, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + StartupTimeout;
        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.HasExited)
                break; // e.g. an updater stub that started the game and left
            if (process.MainWindowHandle != IntPtr.Zero)
                break;
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        await Task.Delay(IpcWarmUp, cancellationToken).ConfigureAwait(false);
    }
}
