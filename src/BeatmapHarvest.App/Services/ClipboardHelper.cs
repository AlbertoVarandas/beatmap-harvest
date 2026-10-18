using System.Windows;

namespace BeatmapHarvest.App.Services;

public static class ClipboardHelper
{
    /// <summary>Copies text on the WPF UI thread (the clipboard needs an STA thread).</summary>
    public static void Copy(string text) => Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
}
