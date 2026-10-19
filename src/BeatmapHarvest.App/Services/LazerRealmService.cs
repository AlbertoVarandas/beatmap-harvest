using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <param name="Content">Null when osu!lazer is not installed or its database could not be read.</param>
public sealed record LazerRealmResult(LazerRealmContent? Content, string? Error);

/// <summary>
/// osu!lazer's own collections and skins, read from a copy of its client.realm (see <see cref="LazerRealmReader"/>).
/// Read once in the background and again after the library reloads.
/// </summary>
public sealed class LazerRealmService
{
    private readonly OsuLibrary _library;
    private readonly AppPaths _paths;
    private readonly Localizer _l;
    private readonly Lock _lock = new();
    private Task<LazerRealmResult>? _result;

    public LazerRealmService(OsuLibrary library, AppPaths paths, Localizer l)
    {
        _library = library;
        _paths = paths;
        _l = l;
        library.Changed += Reset;
    }

    public Task<LazerRealmResult> GetAsync()
    {
        lock (_lock)
            return _result ??= Task.Run(Load);
    }

    private void Reset()
    {
        lock (_lock)
            _result = null;
    }

    private LazerRealmResult Load()
    {
        var lazer = _library.Clients.Lazer;
        if (lazer is null)
            return new LazerRealmResult(null, null);
        try
        {
            return new LazerRealmResult(LazerRealmReader.Read(lazer.RealmFile, _paths.LazerRealmCopyDirectory), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return new LazerRealmResult(null, _l.F("Could not read osu!lazer's database ({0}).", ex.Message));
        }
    }
}
