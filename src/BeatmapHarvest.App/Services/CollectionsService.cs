using System.Diagnostics;
using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.App.Services;

/// <summary>
/// Edits osu! stable's collections. Changes stay in memory until <see cref="Save"/>, which needs osu! to be
/// closed (osu! rewrites collection.db when it exits) and backs up the previous file first.
/// </summary>
public sealed class CollectionsService(OsuLibrary library, MapDetailsService details, TimeProvider time)
{
    private CollectionDatabase? _db;
    private string? _path;

    public event Action? Changed;

    /// <summary>Unsaved changes.</summary>
    public bool IsDirty { get; private set; }

    public string? FilePath => _path;

    public static bool IsOsuRunning()
    {
        var processes = Process.GetProcessesByName("osu!");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    /// <summary>The collections (loaded from osu! on first use). Null when osu! stable is not installed.</summary>
    public CollectionDatabase? Get()
    {
        if (_db is not null)
            return _db;
        var install = library.Install;
        if (install is null)
            return null;
        _path = install.CollectionFile;
        _db = CollectionDatabase.Read(_path);
        return _db;
    }

    /// <summary>Forgets unsaved changes and reads the file again.</summary>
    public void Discard()
    {
        _db = null;
        IsDirty = false;
        Get();
        Changed?.Invoke();
    }

    public OsuCollection Create(string name)
    {
        var db = Get() ?? throw new InvalidOperationException("osu! stable was not found.");
        var existing = db.Collections.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return existing;
        var collection = new OsuCollection(name);
        db.Collections.Add(collection);
        MarkDirty();
        return collection;
    }

    public void Rename(OsuCollection collection, string name)
    {
        collection.Name = name;
        MarkDirty();
    }

    public void Delete(OsuCollection collection)
    {
        Get()?.Collections.Remove(collection);
        MarkDirty();
    }

    public void Remove(OsuCollection collection, string hash)
    {
        if (collection.Hashes.Remove(hash))
            MarkDirty();
    }

    public sealed record AddResult(int Added, int Sets, int NotFound);

    /// <summary>
    /// Adds every difficulty of the given beatmapsets. Installed ones are taken from osu!.db; with
    /// <paramref name="includeMissing"/>, the others are looked up on osu! (about one per second) so they
    /// show up in the collection once downloaded.
    /// </summary>
    public async Task<AddResult> AddAsync(string collectionName, IReadOnlyCollection<MapsetInfo> mapsets, bool includeMissing,
        IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var collection = Create(collectionName);
        var snapshot = await library.GetAsync().ConfigureAwait(false);
        var local = (snapshot.Database?.Beatmaps ?? [])
            .Where(b => b.BeatmapsetId > 0)
            .ToLookup(b => b.BeatmapsetId, b => b.Md5);

        var hashes = new List<string>();
        var notFound = 0;
        var done = 0;
        foreach (var mapset in mapsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (local.Contains(mapset.Id))
            {
                hashes.AddRange(local[mapset.Id]);
            }
            else if (includeMissing)
            {
                try
                {
                    var set = await details.GetAsync(mapset.Id).ConfigureAwait(false);
                    var checksums = set.Beatmaps.Select(b => b.Checksum).OfType<string>().ToList();
                    if (checksums.Count == 0)
                        notFound++;
                    hashes.AddRange(checksums);
                }
                catch (OsuApiException)
                {
                    notFound++;
                }
            }
            else
            {
                notFound++;
            }
            progress?.Report((++done, mapsets.Count));
        }

        var added = collection.Add(hashes);
        if (added > 0)
            MarkDirty();
        return new AddResult(added, mapsets.Count, notFound);
    }

    /// <summary>Writes collection.db. Returns the backup path.</summary>
    /// <exception cref="InvalidOperationException">osu! is running or not installed.</exception>
    public string? Save()
    {
        if (IsOsuRunning())
            throw new InvalidOperationException("Close osu! first: it overwrites collection.db when it exits.");
        var db = Get() ?? throw new InvalidOperationException("osu! stable was not found.");
        var backup = db.Save(_path!, time.GetUtcNow());
        IsDirty = false;
        Changed?.Invoke();
        return backup;
    }

    private void MarkDirty()
    {
        IsDirty = true;
        Changed?.Invoke();
    }
}
