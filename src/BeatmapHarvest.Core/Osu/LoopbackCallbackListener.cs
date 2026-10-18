using System.Globalization;
using System.Net;
using System.Text;
using System.Web;

namespace BeatmapHarvest.Core.Osu;

/// <summary>
/// Receives the OAuth redirect on <c>http://localhost:{port}/callback</c> and returns its query
/// (<c>code</c> and <c>state</c>, or <c>error</c>). The browser tab gets a short "you can close this" page.
/// </summary>
public sealed class LoopbackCallbackListener : IDisposable
{
    public const int DefaultPort = 7270;

    private readonly HttpListener _listener = new();

    /// <exception cref="HttpListenerException">The port is already in use.</exception>
    public LoopbackCallbackListener(int port = DefaultPort)
    {
        RedirectUri = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/callback"));
        _listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/"));
        _listener.Start();
    }

    /// <summary>Must be registered exactly like this in the OAuth application's callback URLs.</summary>
    public Uri RedirectUri { get; }

    public static Uri RedirectUriFor(int port = DefaultPort) =>
        new(string.Create(CultureInfo.InvariantCulture, $"http://localhost:{port}/callback"));

    public async Task<IReadOnlyDictionary<string, string>> WaitAsync(CancellationToken cancellationToken)
    {
        // GetContextAsync cannot be cancelled; stopping the listener ends it.
        await using var _ = cancellationToken.Register(_listener.Stop).ConfigureAwait(false);
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            using var response = context.Response;
            if (!string.Equals(context.Request.Url?.AbsolutePath, RedirectUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                // e.g. /favicon.ico
                response.StatusCode = 404;
                continue;
            }

            var query = HttpUtility.ParseQueryString(context.Request.Url!.Query);
            var values = query.AllKeys.OfType<string>().ToDictionary(k => k, k => query[k] ?? "", StringComparer.Ordinal);
            await WritePageAsync(response, values.ContainsKey("error")).ConfigureAwait(false);
            return values;
        }
    }

    private static async Task WritePageAsync(HttpListenerResponse response, bool failed)
    {
        var message = failed
            ? "Sign-in was cancelled. You can close this tab."
            : "Signed in. You can close this tab and go back to Beatmap Harvest.";
        var html = $"<!doctype html><meta charset=\"utf-8\"><title>Beatmap Harvest</title>"
                   + "<body style=\"font-family:Segoe UI,sans-serif;background:#1c1719;color:#f0e6ea;display:grid;place-items:center;height:90vh\">"
                   + $"<p style=\"font-size:18px\">{message}</p></body>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    public void Dispose() => ((IDisposable)_listener).Dispose();
}
