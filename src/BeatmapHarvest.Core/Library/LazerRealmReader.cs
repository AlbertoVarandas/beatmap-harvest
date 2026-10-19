using Realms;
using Realms.Exceptions;

namespace BeatmapHarvest.Core.Library;

public sealed record LazerCollectionInfo(Guid Id, string Name, IReadOnlyList<string> Hashes, DateTimeOffset LastModified);

/// <param name="Hash">Name of the file in lazer's store (files/a/ab/abcdef…).</param>
public sealed record LazerSkinFile(string Filename, string Hash);

/// <param name="Protected">One of lazer's built-in skins (they have no files of their own).</param>
public sealed record LazerSkin(Guid Id, string Name, string Creator, bool Protected, IReadOnlyList<LazerSkinFile> Files);

public sealed record LazerRealmContent(IReadOnlyList<LazerCollectionInfo> Collections, IReadOnlyList<LazerSkin> Skins);

/// <summary>
/// Reads collections and skins from osu!lazer's database. It never opens lazer's own client.realm: the file is copied to
/// a work folder and only the copy is opened, through Realm's dynamic API (no model classes, so lazer's frequent schema
/// changes don't matter). Opening the copy may upgrade the copy; the original is never touched.
/// </summary>
public static class LazerRealmReader
{
    // Above any version lazer will reach; opening the copy with it just records a newer version in the copy.
    private const ulong CopySchemaVersion = 1_000_000;

    private static readonly Guid RandomSkinId = Guid.Parse("D39DFEFB-477C-4372-B1EA-2BCEA5FB8908");

    /// <exception cref="InvalidDataException">The copy is not a database Realm can open.</exception>
    public static LazerRealmContent Read(string realmFile, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        var copy = Path.Combine(workDirectory, "client-copy.realm");
        DeleteCopy(copy);
        File.Copy(realmFile, copy, overwrite: true);

        try
        {
            var config = new RealmConfiguration(copy)
            {
                IsDynamic = true,
                SchemaVersion = CopySchemaVersion,
            };
            return WithoutSynchronizationContext(() =>
            {
                using var realm = Realm.GetInstance(config);
                return new LazerRealmContent(ReadCollections(realm), ReadSkins(realm));
            });
        }
        catch (RealmException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
        finally
        {
            DeleteCopy(copy);
        }
    }

    /// <summary>
    /// Realm hooks change notifications into the current SynchronizationContext (the UI thread, a test runner…) and calls
    /// back into it after the realm is gone. A one-shot read needs no notifications, so it runs with no context at all.
    /// </summary>
    internal static T WithoutSynchronizationContext<T>(Func<T> action)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static List<LazerCollectionInfo> ReadCollections(Realm realm)
    {
        if (!HasClass(realm, "BeatmapCollection"))
            return [];
        return realm.DynamicApi.All("BeatmapCollection")
            .ToList()
            .Select(c => new LazerCollectionInfo(
                c.DynamicApi.Get<Guid>("ID"),
                c.DynamicApi.Get<string>("Name") ?? "",
                c.DynamicApi.GetList<string>("BeatmapMD5Hashes").ToList(),
                c.DynamicApi.Get<DateTimeOffset>("LastModified")))
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static List<LazerSkin> ReadSkins(Realm realm)
    {
        if (!HasClass(realm, "Skin"))
            return [];
        var skins = new List<LazerSkin>();
        foreach (var skin in realm.DynamicApi.All("Skin").ToList())
        {
            // "<Random skin>" is a menu entry, not a skin.
            if (skin.DynamicApi.Get<bool>("DeletePending") || skin.DynamicApi.Get<Guid>("ID") == RandomSkinId)
                continue;
            var files = new List<LazerSkinFile>();
            foreach (var usage in skin.DynamicApi.GetList<IEmbeddedObject>("Files"))
            {
                var name = usage.DynamicApi.Get<string>("Filename");
                var hash = usage.DynamicApi.Get<IRealmObject>("File")?.DynamicApi.Get<string>("Hash");
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(hash))
                    files.Add(new LazerSkinFile(name, hash));
            }
            skins.Add(new LazerSkin(
                skin.DynamicApi.Get<Guid>("ID"),
                skin.DynamicApi.Get<string>("Name") ?? "",
                skin.DynamicApi.Get<string>("Creator") ?? "",
                skin.DynamicApi.Get<bool>("Protected"),
                files));
        }
        return skins
            .OrderBy(s => s.Protected ? 0 : 1)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool HasClass(Realm realm, string name) => realm.Schema.TryFindObjectSchema(name, out _);

    /// <summary>Path of a file in lazer's store, relative to its <c>files</c> folder: a/ab/abcdef….</summary>
    public static string StorePath(string hash) => Path.Combine(hash[..1], hash[..2], hash);

    private static void DeleteCopy(string copy)
    {
        foreach (var path in new[] { copy, copy + ".lock", copy + ".note" })
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // Overwritten on the next read.
            }
        }
        var management = copy + ".management";
        if (Directory.Exists(management))
        {
            try
            {
                Directory.Delete(management, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
