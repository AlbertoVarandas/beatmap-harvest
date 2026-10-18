using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.Core.Library;

/// <summary>
/// <c>done.json</c>: beatmapset ids whose download finished. An id is added only after the .osz is in place,
/// and the file is rewritten atomically, so an interrupted run resumes where it stopped. Thread-safe.
/// </summary>
public sealed class DownloadLedger
{
    private readonly string _path;
    private readonly HashSet<long> _ids;
    private readonly Lock _lock = new();

    public DownloadLedger(string path)
    {
        _path = path;
        _ids = AtomicFile.ReadJsonOrDefault(path, new HashSet<long>());
    }

    public bool Contains(long setId)
    {
        lock (_lock)
            return _ids.Contains(setId);
    }

    public IReadOnlySet<long> Snapshot()
    {
        lock (_lock)
            return new HashSet<long>(_ids);
    }

    public void MarkDone(long setId)
    {
        lock (_lock)
        {
            if (_ids.Add(setId))
                AtomicFile.WriteJson(_path, _ids.Order().ToList());
        }
    }
}
