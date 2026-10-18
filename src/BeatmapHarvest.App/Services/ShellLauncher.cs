using System.Diagnostics;

namespace BeatmapHarvest.App.Services;

/// <summary>Opens URLs in the default browser and folders in Explorer.</summary>
public static class ShellLauncher
{
    public static void Open(string target)
    {
        using var _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Open(path);
    }
}
