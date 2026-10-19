using Realms;
using Realms.Exceptions;
using Realms.Schema;

namespace BeatmapHarvest.Core.Library;

/// <summary>A collection to put into osu!lazer: created if no collection has this name, otherwise its maps are added.</summary>
public sealed record LazerCollectionWrite(string Name, IReadOnlyCollection<string> Hashes);

/// <param name="Backup">Copy of client.realm from right before the change.</param>
public sealed record LazerWriteResult(int Created, int Updated, int HashesAdded, string Backup);

/// <summary>Whether collections can be written into this client.realm, and if not, why (an English UI text).</summary>
public sealed record LazerWriteCompatibility(bool Compatible, string? Reason);

/// <summary>
/// EXPERIMENTAL: adds collections straight into osu!lazer's client.realm, which lazer must have closed. Never edits the
/// file in place: the change is made in a copy, opened with the schema already in the file (so neither the schema nor
/// its version change), checked by reading it back, and only then swapped in, after a backup. It only adds collections or maps to
/// collections, the same way lazer's own import merges them by name; nothing is ever removed.
/// </summary>
public static class LazerRealmWriter
{
    private const int BackupsKept = 5;

    /// <summary>The format this app's Realm writes. A file in any other format is never touched: opening it would convert it.</summary>
    public static int OwnFileFormat(string workDirectory)
    {
        lock (FormatLock)
        {
            if (_ownFileFormat is { } known)
                return known;
            Directory.CreateDirectory(workDirectory);
            var probe = Path.Combine(workDirectory, "format-probe.realm");
            LazerRealmReader.DeleteCopy(probe);
            try
            {
                LazerRealmReader.WithoutSynchronizationContext(() =>
                {
                    using var realm = Realm.GetInstance(new RealmConfiguration(probe) { IsDynamic = true });
                    realm.Write(() => { }); // makes sure the file is fully written
                    return 0;
                });
                _ownFileFormat = RealmFileHeader.FileFormat(probe) ?? -1;
                return _ownFileFormat.Value;
            }
            finally
            {
                LazerRealmReader.DeleteCopy(probe);
            }
        }
    }

    private static readonly Lock FormatLock = new();
    private static int? _ownFileFormat;

    public static LazerWriteCompatibility Compatibility(LazerRealmContent content, string workDirectory)
    {
        if (content.FileFormat is null)
            return new(false, "osu!lazer's database could not be read.");
        if (content.FileFormat != OwnFileFormat(workDirectory))
            return new(false, "This osu!lazer stores its database in a format Beatmap Harvest does not know yet. Update Beatmap Harvest, or use the setup wizard.");
        if (!content.CollectionsShapeOk)
            return new(false, "This osu!lazer stores collections differently from what Beatmap Harvest knows. Update Beatmap Harvest, or use the setup wizard.");
        return new(true, null);
    }

    /// <summary>BeatmapCollection has the properties written here, with the expected types (and nothing required that isn't).</summary>
    internal static bool HasCollectionShape(Realm realm)
    {
        if (!realm.Schema.TryFindObjectSchema("BeatmapCollection", out var schema))
            return false;
        bool Has(string name, PropertyType type) =>
            schema.TryFindProperty(name, out var p) && (p.Type & ~PropertyType.Nullable) == type;
        var known = new[] { "ID", "Name", "BeatmapMD5Hashes", "LastModified" };
        return Has("ID", PropertyType.Guid)
            && schema.TryFindProperty("ID", out var id) && id.IsPrimaryKey
            && Has("Name", PropertyType.String)
            && Has("BeatmapMD5Hashes", PropertyType.Array | PropertyType.String)
            && Has("LastModified", PropertyType.Date)
            // A new property lazer would need us to fill in: refuse rather than guess.
            && schema.All(p => known.Contains(p.Name) || p.Type.HasFlag(PropertyType.Nullable) || p.Type.HasFlag(PropertyType.Array) || p.Type.HasFlag(PropertyType.Set) || p.Type.HasFlag(PropertyType.Dictionary));
    }

    /// <exception cref="InvalidOperationException">The file is not compatible, changed meanwhile, or the result did not check out (the original is then untouched).</exception>
    public static LazerWriteResult AddCollections(string realmFile, string workDirectory, string backupDirectory,
        IReadOnlyList<LazerCollectionWrite> collections, DateTimeOffset now)
    {
        Directory.CreateDirectory(workDirectory);
        var before = Stamp(realmFile);
        var work = Path.Combine(workDirectory, "client-write.realm");
        LazerRealmReader.DeleteCopy(work);
        File.Copy(realmFile, work, overwrite: true);
        try
        {
            // 1. What lazer has: its version and format, from a separate throwaway copy.
            var content = LazerRealmReader.Read(work, Path.Combine(workDirectory, "check"));
            var compatibility = Compatibility(content, workDirectory);
            if (!compatibility.Compatible)
                throw new InvalidOperationException(compatibility.Reason);

            // 2. The change, in the copy. Opened dynamically, with the schema already in the file, so Realm changes nothing but the data.
            var (created, updated, added) = Write(work, collections, now);
            LazerRealmReader.DeleteCopy(work, keepMain: true);

            // 3. Read the result back: same version and format, and every collection holds what it should.
            var check = LazerRealmReader.Read(work, Path.Combine(workDirectory, "check"));
            if (check.SchemaVersion != content.SchemaVersion || check.FileFormat != content.FileFormat)
                throw new InvalidOperationException("The changed database did not check out, so osu!lazer was left as it was.");
            foreach (var wanted in collections)
            {
                var written = check.Collections.FirstOrDefault(c => c.Name == wanted.Name);
                if (written is null || !wanted.Hashes.All(written.Hashes.Contains))
                    throw new InvalidOperationException("The changed database did not check out, so osu!lazer was left as it was.");
            }

            // 4. Nobody (osu!lazer) changed the original meanwhile.
            if (Stamp(realmFile) != before)
                throw new InvalidOperationException("osu!lazer's database changed while saving. Close osu!lazer and try again.");

            // 5. Backup, then swap the checked copy in. File.Replace is a rename on the same folder, so the file is
            //    never half-written; if anything fails, the backup goes back.
            var backup = Backup(realmFile, backupDirectory, now);
            var incoming = realmFile + ".beatmapharvest-new";
            try
            {
                File.Copy(work, incoming, overwrite: true);
                File.Replace(incoming, realmFile, destinationBackupFileName: null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(incoming);
                if (Stamp(realmFile) != before)
                    File.Copy(backup, realmFile, overwrite: true);
                throw new InvalidOperationException("osu!lazer's database could not be replaced; it was left as it was.", ex);
            }
            return new LazerWriteResult(created, updated, added, backup);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("osu!lazer's database could not be read.", ex);
        }
        finally
        {
            LazerRealmReader.DeleteCopy(work);
        }
    }

    private static (int Created, int Updated, int Added) Write(string path,
        IReadOnlyList<LazerCollectionWrite> collections, DateTimeOffset now)
    {
        try
        {
            return LazerRealmReader.WithoutSynchronizationContext(() =>
            {
                using var realm = Realm.GetInstance(new RealmConfiguration(path) { IsDynamic = true });
                if (!HasCollectionShape(realm))
                    throw new InvalidOperationException("This osu!lazer stores collections differently from what Beatmap Harvest knows. Update Beatmap Harvest, or use the setup wizard.");
                int created = 0, updated = 0, added = 0;
                realm.Write(() =>
                {
                    var existing = realm.DynamicApi.All("BeatmapCollection").ToList();
                    foreach (var wanted in collections)
                    {
                        var target = existing.FirstOrDefault(c => c.DynamicApi.Get<string>("Name") == wanted.Name);
                        var isNew = target is null;
                        if (target is null)
                        {
                            target = (IRealmObject)realm.DynamicApi.CreateObject("BeatmapCollection", Guid.NewGuid());
                            target.DynamicApi.Set("Name", wanted.Name);
                            existing.Add(target);
                        }
                        var hashes = target.DynamicApi.GetList<string>("BeatmapMD5Hashes");
                        var have = hashes.ToHashSet(StringComparer.Ordinal);
                        var addedHere = 0;
                        foreach (var hash in wanted.Hashes)
                        {
                            if (have.Add(hash))
                            {
                                hashes.Add(hash);
                                addedHere++;
                            }
                        }
                        if (isNew || addedHere > 0)
                            target.DynamicApi.Set("LastModified", now);
                        added += addedHere;
                        if (isNew)
                            created++;
                        else if (addedHere > 0)
                            updated++;
                    }
                });
                return (created, updated, added);
            });
        }
        catch (RealmException ex)
        {
            throw new InvalidOperationException("The change could not be made, so osu!lazer was left as it was.", ex);
        }
    }

    private static string Backup(string realmFile, string backupDirectory, DateTimeOffset now)
    {
        Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"client.realm.{now.ToLocalTime():yyyyMMdd-HHmmss}.bak");
        File.Copy(realmFile, backup, overwrite: true);
        foreach (var old in Directory.GetFiles(backupDirectory, "client.realm.*.bak").OrderDescending(StringComparer.Ordinal).Skip(BackupsKept))
            TryDelete(old);
        return backup;
    }

    private static (long Length, DateTime Written) Stamp(string path)
    {
        var info = new FileInfo(path);
        return (info.Length, info.LastWriteTimeUtc);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
