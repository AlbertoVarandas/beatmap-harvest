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
        var root = OsuInstall.Find(settings.Load().OsuDirectory)?.Root;
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_webView is null)
                return;
            _webView.ClearVirtualHostNameToFolderMapping(HostName);
            if (root is not null)
                _webView.SetVirtualHostNameToFolderMapping(HostName, root, CoreWebView2HostResourceAccessKind.Allow);
        });
    }

    /// <summary>URL of a file given relative to the osu! folder, e.g. <c>Skins/My Skin/cursor.png</c>.</summary>
    public static string Url(string relativePath) =>
        $"https://{HostName}/" + string.Join('/', relativePath.Split('\\', '/').Select(Uri.EscapeDataString));
}
