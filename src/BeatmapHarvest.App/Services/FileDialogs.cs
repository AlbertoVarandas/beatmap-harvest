using System.Windows;
using Microsoft.Win32;

namespace BeatmapHarvest.App.Services;

/// <summary>Native Save/Open dialogs, shown on the WPF UI thread and owned by the main window.</summary>
public static class FileDialogs
{
    public const string JsonFilter = "Selector config (*.json)|*.json|All files (*.*)|*.*";

    public static string? Save(string title, string fileName, string filter) =>
        Show(new SaveFileDialog { Title = title, FileName = fileName, Filter = filter, AddExtension = true, DefaultExt = ".json" });

    public static string? Open(string title, string filter) =>
        Show(new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true });

    private static string? Show(FileDialog dialog) =>
        Application.Current.Dispatcher.Invoke(() =>
            dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null);
}
