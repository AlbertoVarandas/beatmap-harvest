using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Osu;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.App.Services;

/// <summary>Where the collections are written.</summary>
public enum CollectionsTarget
{
    /// <summary>No osu! found.</summary>
    None,

    /// <summary>osu! stable's own collection.db; osu! shows them directly.</summary>
    Stable,

    /// <summary>osu!lazer is used and osu! stable is installed: written to stable's collection.db, from which lazer imports them.</summary>
    LazerViaStable,

    /// <summary>osu!lazer only: written to a folder of the app that lazer accepts as an "osu!stable" folder to import from.</summary>
    LazerFolder,
}

public sealed record CollectionsLocation(CollectionsTarget Target, string File, string Folder);

/// <summary>
/// Edits collections in osu! stable's collection.db format. With osu!lazer, which keeps its collections in its own
/// database (never changed by this app), the file is the one lazer imports through "Import from osu!stable".
/// Changes stay in memory until <see cref="Save"/>, which backs up the previous file first.
/// </summary>
public sealed class CollectionsService(OsuLibrary library, MapDetailsService details, AppPaths paths, TimeProvider time)
{
    private CollectionDatabase? _db;
    private CollectionsLocation? _location;

    public event Action? Changed;

    /// <summary>Unsaved changes.</summary>
    public bool IsDirty { get; private set; }

    public CollectionsLocation Location => _location ??= Resolve();

    /// <summary>osu! stable rewrites its collection.db when it exits, so saving into it needs it closed.</summary>
    public bool SaveBlockedByOsu =>
        Location.Target is CollectionsTarget.Stable or CollectionsTarget.LazerViaStable && OsuProcesses.IsRunning(OsuClient.Stable);

    /// <summary>The collections, loaded on first use. Null when no osu! is installed.</summary>
    public CollectionDatabase? Get()
    {
        if (_db is not null)
            return _db;
        var location = Location;
        if (location.Target == CollectionsTarget.None)
            return null;
        _db = CollectionDatabase.Read(location.File);
        return _db;
    }

    /// <summary>Forgets unsaved changes and reads the file again (also after the osu! client changed in Settings).</summary>
    public void Discard()
    {
        _db = null;
        _location = null;
        IsDirty = false;
        Get();
        Changed?.Invoke();
    }

    public OsuCollection Create(string name)
    {
        var db = Get() ?? throw new InvalidOperationException("osu! was not found on this PC.");
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
    /// Adds every difficulty of the given beatmapsets. Installed ones are taken from the game's own files; with
    /// <paramref name="includeMissing"/>, the others are looked up on osu! (about one per second) so they
    /// show up in the collection once they are in the game.
    /// </summary>
    public async Task<AddResult> AddAsync(string collectionName, IReadOnlyCollection<MapsetInfo> mapsets, bool includeMissing,
        IProgress<(int Done, int Total)>? progress, CancellationToken cancellationToken)
    {
        var collection = Create(collectionName);
        var snapshot = await library.GetAsync().ConfigureAwait(false);
        var local = snapshot.Maps
            .Where(b => b.BeatmapsetId > 0 && b.Md5.Length > 0)
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

    /// <summary>Writes the collection.db. Returns the backup path, if there was a previous file.</summary>
    /// <exception cref="InvalidOperationException">osu! stable is running, or no osu! is installed.</exception>
    public string? Save()
    {
        if (SaveBlockedByOsu)
            throw new InvalidOperationException("Close osu! first: it overwrites collection.db when it exits.");
        var db = Get() ?? throw new InvalidOperationException("osu! was not found on this PC.");
        var location = Location;
        if (location.Target == CollectionsTarget.LazerFolder)
        {
            // osu!lazer only offers a folder for "Import from osu!stable" if it looks like a stable install.
            Directory.CreateDirectory(Path.Combine(location.Folder, "Songs"));
        }
        var backup = db.Save(location.File, time.GetUtcNow());
        IsDirty = false;
        Changed?.Invoke();
        return backup;
    }

    private CollectionsLocation Resolve()
    {
        var clients = library.Clients;
        var stable = clients.Stable;
        return library.Client switch
        {
            OsuClient.Stable when stable is not null => new(CollectionsTarget.Stable, stable.CollectionFile, stable.Root),
            OsuClient.Lazer when stable is not null => new(CollectionsTarget.LazerViaStable, stable.CollectionFile, stable.Root),
            OsuClient.Lazer => new(CollectionsTarget.LazerFolder,
                Path.Combine(paths.LazerCollectionsDirectory, "collection.db"), paths.LazerCollectionsDirectory),
            _ => new(CollectionsTarget.None, "", ""),
        };
    }

    private void MarkDirty()
    {
        IsDirty = true;
        Changed?.Invoke();
    }
}
