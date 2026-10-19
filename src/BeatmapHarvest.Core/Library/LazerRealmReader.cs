using Realms;
using Realms.Exceptions;
using Realms.Schema;

namespace BeatmapHarvest.Core.Library;

public sealed record LazerCollectionInfo(Guid Id, string Name, IReadOnlyList<string> Hashes, DateTimeOffset LastModified);

/// <param name="Hash">Name of the file in lazer's store (files/a/ab/abcdef…).</param>
public sealed record LazerSkinFile(string Filename, string Hash);

/// <param name="Protected">One of lazer's built-in skins (they have no files of their own).</param>
public sealed record LazerSkin(Guid Id, string Name, string Creator, bool Protected, IReadOnlyList<LazerSkinFile> Files);

/// <param name="Backgrounds">For each beatmapset id, the store hash of its background image (for maps without a cover online).</param>
/// <param name="SchemaVersion">lazer's database version (its model classes; 52 in late 2026); null if unknown.</param>
/// <param name="FileFormat">Realm's on-disk format of the file, from its header; null if the header is not Realm's.</param>
/// <param name="CollectionsShapeOk">BeatmapCollection has exactly the properties <see cref="LazerRealmWriter"/> writes.</param>
public sealed record LazerRealmContent(
    IReadOnlyList<LazerCollectionInfo> Collections,
    IReadOnlyList<LazerSkin> Skins,
    IReadOnlyDictionary<long, string> Backgrounds,
    ulong? SchemaVersion,
    int? FileFormat,
    bool CollectionsShapeOk);

/// <summary>
/// Reads collections and skins from osu!lazer's database. It never opens lazer's own client.realm: the file is copied to
/// a work folder and only the copy is opened, through Realm's dynamic API (no model classes, so lazer's frequent schema
/// changes don't matter). The original is never opened.
/// </summary>
public static partial class LazerRealmReader
{
    private static readonly Guid RandomSkinId = Guid.Parse("D39DFEFB-477C-4372-B1EA-2BCEA5FB8908");

    /// <exception cref="InvalidDataException">The copy is not a database Realm can open.</exception>
    public static LazerRealmContent Read(string realmFile, string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        var copy = Path.Combine(workDirectory, "client-copy.realm");
        DeleteCopy(copy);
        File.Copy(realmFile, copy, overwrite: true);
        var fileFormat = RealmFileHeader.FileFormat(copy);

        try
        {
            // Dynamic: the file's own schema is used and its version is left as it is (Realm ignores SchemaVersion here).
            var config = new RealmConfiguration(copy) { IsDynamic = true };
            return WithoutSynchronizationContext(() =>
            {
                LazerRealmContent content;
                RealmSchema schema;
                using (var realm = Realm.GetInstance(config))
                {
                    schema = realm.Schema;
                    content = new LazerRealmContent(ReadCollections(realm), ReadSkins(realm), ReadBackgrounds(realm),
                        null, fileFormat, LazerRealmWriter.HasCollectionShape(realm));
                }
                return content with { SchemaVersion = SchemaVersionOf(copy, schema) };
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
    /// The schema version stored in a copy (shown to the user). Realm has no call for it, but opening the file with its
    /// own schema at version 0 is refused with "… less than last set version N" before anything is written.
    /// </summary>
    private static ulong? SchemaVersionOf(string copy, RealmSchema schema)
    {
        try
        {
            using var realm = Realm.GetInstance(new RealmConfiguration(copy) { Schema = schema, SchemaVersion = 0 });
            return 0;
        }
        catch (RealmException ex) when (StoredVersion().Match(ex.Message) is { Success: true } match)
        {
            return ulong.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (RealmException)
        {
            return null;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"last set version (\d+)")]
    private static partial System.Text.RegularExpressions.Regex StoredVersion();

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

    /// <summary>
    /// BeatmapSet → first Beatmap → Metadata.BackgroundFile, looked up in the set's Files. Left empty if lazer's schema
    /// no longer has these properties: the backgrounds are only a fallback image.
    /// </summary>
    private static Dictionary<long, string> ReadBackgrounds(Realm realm)
    {
        var backgrounds = new Dictionary<long, string>();
        if (!HasProperties(realm, "BeatmapSet", "OnlineID", "Beatmaps", "Files", "DeletePending")
            || !HasProperties(realm, "Beatmap", "Metadata")
            || !HasProperties(realm, "BeatmapMetadata", "BackgroundFile"))
            return backgrounds;

        foreach (var set in realm.DynamicApi.All("BeatmapSet").ToList())
        {
            var id = set.DynamicApi.Get<int>("OnlineID");
            if (id <= 0 || set.DynamicApi.Get<bool>("DeletePending"))
                continue;
            var background = set.DynamicApi.GetList<IRealmObject>("Beatmaps")
                .Select(b => b.DynamicApi.Get<IRealmObject>("Metadata")?.DynamicApi.Get<string>("BackgroundFile"))
                .FirstOrDefault(f => !string.IsNullOrEmpty(f));
            if (background is null)
                continue;
            foreach (var usage in set.DynamicApi.GetList<IEmbeddedObject>("Files"))
            {
                if (string.Equals(usage.DynamicApi.Get<string>("Filename"), background, StringComparison.OrdinalIgnoreCase)
                    && usage.DynamicApi.Get<IRealmObject>("File")?.DynamicApi.Get<string>("Hash") is { Length: > 0 } hash)
                {
                    backgrounds.TryAdd(id, hash);
                    break;
                }
            }
        }
        return backgrounds;
    }

    private static bool HasClass(Realm realm, string name) => realm.Schema.TryFindObjectSchema(name, out _);

    private static bool HasProperties(Realm realm, string className, params string[] properties) =>
        realm.Schema.TryFindObjectSchema(className, out var schema) && properties.All(p => schema.TryFindProperty(p, out _));

    /// <summary>Path of a file in lazer's store, relative to its <c>files</c> folder: a/ab/abcdef….</summary>
    public static string StorePath(string hash) => Path.Combine(hash[..1], hash[..2], hash);

    /// <summary>Deletes a copy and the files Realm keeps next to an open file (<paramref name="keepMain"/>: only those).</summary>
    internal static void DeleteCopy(string copy, bool keepMain = false)
    {
        foreach (var path in keepMain ? [copy + ".lock", copy + ".note"] : new[] { copy, copy + ".lock", copy + ".note" })
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
