using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.FileProviders;

namespace BeatmapHarvest.App;

/// <summary>
/// Serves the UI (wwwroot) from resources embedded in this assembly instead of loose files,
/// so a single-file BeatmapHarvest.exe works on its own. The csproj also embeds Blazor's
/// _framework/blazor.webview.js from the WebView package; wwwroot/_framework/blazor.modules.json
/// (no JS initializers) replaces the one static web assets would generate.
/// </summary>
public sealed class EmbeddedBlazorWebView : BlazorWebView
{
    public override IFileProvider CreateFileProvider(string contentRootDir) =>
        new ManifestEmbeddedFileProvider(typeof(EmbeddedBlazorWebView).Assembly, "wwwroot");
}
