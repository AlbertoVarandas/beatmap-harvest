using System.Windows;
using BeatmapHarvest.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BeatmapHarvest.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = new ServiceCollection().AddBeatmapHarvest().BuildServiceProvider();
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
