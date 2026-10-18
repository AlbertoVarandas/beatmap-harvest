using System.Windows;
using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Serves files from the osu! folder (skin images) to the UI at <c>https://osu.local/…</c>,
/// through WebView2's virtual host mapping. Read-only; nothing outside the osu! folder is reachable.
/// </summary>
public sealed class OsuFilesHost(SettingsStore settings)
{
    public const string HostName = "osu.local";

    /// <summary>The Songs folder, which may live outside the osu! folder (BeatmapDirectory in osu!'s config).</summary>
    public const string SongsHostName = "songs.local";

    private CoreWebView2? _webView;

    /// <summary>Called once the WebView exists.</summary>
    public void Attach(CoreWebView2 webView)
    {
        _webView = webView;
        Refresh();
    }

    /// <summary>Points the mapping at the current osu! folder (after it changes in Settings).</summary>
    public void Refresh()
    {
        var current = settings.Load();
        var install = OsuInstall.Find(current.OsuDirectory);
        var songs = current.SongsDirectory ?? install?.SongsDirectory;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_webView is null)
                return;
            Map(_webView, HostName, install?.Root);
            Map(_webView, SongsHostName, songs);
        });
    }

    private static void Map(CoreWebView2 webView, string host, string? folder)
    {
        webView.ClearVirtualHostNameToFolderMapping(host);
        if (folder is not null && Directory.Exists(folder))
            webView.SetVirtualHostNameToFolderMapping(host, folder, CoreWebView2HostResourceAccessKind.Allow);
    }

    /// <summary>URL of a file given relative to the osu! folder, e.g. <c>Skins/My Skin/cursor.png</c>.</summary>
    public static string Url(string relativePath) => BuildUrl(HostName, relativePath);

    /// <summary>URL of a file given relative to the Songs folder, e.g. <c>123 Artist - Title/bg.jpg</c>.</summary>
    public static string SongsUrl(string relativePath) => BuildUrl(SongsHostName, relativePath);

    private static string BuildUrl(string host, string relativePath) =>
        $"https://{host}/" + string.Join('/', relativePath.Split('\\', '/').Select(Uri.EscapeDataString));
}
