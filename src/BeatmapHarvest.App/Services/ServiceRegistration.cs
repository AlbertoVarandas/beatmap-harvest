using System.Net;
using BeatmapHarvest.Core.Downloads;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace BeatmapHarvest.App.Services;

public static class ServiceRegistration
{
    public const string OsuHttp = "osu";
    public const string MirrorHttp = "mirrors";

    private static readonly string UserAgent =
        $"BeatmapHarvest/{typeof(ServiceRegistration).Assembly.GetName().Version?.ToString(3)} (+https://github.com/AlbertoVarandas/beatmap-harvest)";

    public static IServiceCollection AddBeatmapHarvest(this IServiceCollection services)
    {
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        services.AddSingleton(AppPaths.Default);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new OsuApiOptions());
        services.AddSingleton<ICredentialStore, DpapiCredentialStore>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<HarvestStore>();
        services.AddSingleton(sp => new DownloadLedger(sp.GetRequiredService<AppPaths>().LedgerFile));

        // One long-lived client per destination. The API client must be a singleton anyway (it owns the rate limiter).
        services.AddKeyedSingleton(OsuHttp, (_, _) => CreateHttpClient(TimeSpan.FromSeconds(30)));
        services.AddKeyedSingleton(MirrorHttp, (_, _) => CreateHttpClient(TimeSpan.FromMinutes(5)));

        services.AddSingleton(sp => new OsuTokenProvider(
            sp.GetRequiredKeyedService<HttpClient>(OsuHttp),
            sp.GetRequiredService<ICredentialStore>(),
            sp.GetRequiredService<OsuApiOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<OsuTokenProvider>());
        services.AddSingleton<IOsuDataSource>(sp => new OsuApiClient(
            sp.GetRequiredKeyedService<HttpClient>(OsuHttp),
            sp.GetRequiredService<IAccessTokenProvider>(),
            sp.GetRequiredService<OsuApiOptions>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ProfileHarvester>();

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<SettingsStore>();
            return new BeatmapDownloader(
                sp.GetRequiredKeyedService<HttpClient>(MirrorHttp),
                () => settings.Load().Mirrors,
                sp.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<DownloadRunner>();

        services.AddSingleton<Localizer>();
        services.AddSingleton<SetupState>();
        services.AddSingleton<ProfileLibrary>();
        services.AddSingleton<HarvestSession>();
        services.AddSingleton<SelectorState>();
        services.AddSingleton<DownloadSession>();
        services.AddSingleton<MapDetailsService>();
        services.AddSingleton<OsuAccount>();
        services.AddSingleton<OsuLibrary>();
        services.AddSingleton<OsuFilesHost>();
        services.AddSingleton<CollectionsService>();
        return services;
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            // Long-lived client: recycle connections so DNS changes are picked up.
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        };
        var client = new HttpClient(handler) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}
