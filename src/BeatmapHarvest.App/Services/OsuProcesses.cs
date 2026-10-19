using System.ComponentModel;
using System.Diagnostics;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.App.Services;

/// <summary>Finds running osu! clients. Both are called "osu!", so they are told apart by where the executable lives.</summary>
public static class OsuProcesses
{
    public static bool IsRunning(OsuClient client)
    {
        var processes = Process.GetProcessesByName("osu!");
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
                    return true;
                }
                var isLazer = path?.Contains("osulazer", StringComparison.OrdinalIgnoreCase) == true;
                if (isLazer == (client == OsuClient.Lazer))
                    return true;
            }
            return false;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }
}
