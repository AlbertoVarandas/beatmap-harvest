using System.Windows;
using BeatmapHarvest.App.Services;
using BeatmapHarvest.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BeatmapHarvest.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Keep WebView2's cache with the rest of the app data instead of a separate %LOCALAPPDATA%\BeatmapHarvest.WebView2.
        // An explicit WEBVIEW2_USER_DATA_FOLDER (e.g. for testing) still wins.
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER")))
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", Path.Combine(AppPaths.Default.Root, "WebView2"));

        _services = new ServiceCollection().AddBeatmapHarvest().BuildServiceProvider();
        // Dates and numbers follow the chosen interface language from the start.
        _services.GetRequiredService<Localizer>().ApplyCulture();
        // MainWindow's BlazorWebView binds to this resource.
        Resources.Add("services", _services);

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
