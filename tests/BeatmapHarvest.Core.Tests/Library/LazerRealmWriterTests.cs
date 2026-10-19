using System.Security.Cryptography;
using BeatmapHarvest.Core.Library;
using Realms;
using Realms.Schema;

namespace BeatmapHarvest.Core.Tests.Library;

public class LazerRealmWriterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static RealmSchema Schema(bool extraRequiredProperty = false)
    {
        var collection = new ObjectSchema.Builder("BeatmapCollection", ObjectSchema.ObjectType.RealmObject)
        {
            Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
            Property.Primitive("Name", RealmValueType.String, isNullable: true),
            Property.PrimitiveList("BeatmapMD5Hashes", RealmValueType.String, areElementsNullable: true),
            Property.Primitive("LastModified", RealmValueType.Date),
        };
        if (extraRequiredProperty)
            collection.Add(Property.Primitive("Ruleset", RealmValueType.Int));
        return new RealmSchema.Builder { collection }.Build();
    }

    /// <summary>A client.realm at schema version 52 with one collection, like lazer's.</summary>
    private static string CreateRealm(string directory, bool extraRequiredProperty = false)
    {
        var path = Path.Combine(directory, "client.realm");
        LazerRealmReader.WithoutSynchronizationContext(() =>
        {
            using var realm = Realm.GetInstance(new RealmConfiguration(path) { Schema = Schema(extraRequiredProperty), SchemaVersion = 52 });
            realm.Write(() =>
            {
                var existing = realm.DynamicApi.CreateObject("BeatmapCollection", Guid.NewGuid());
                existing.DynamicApi.Set("Name", "Favourites");
                existing.DynamicApi.Set("LastModified", DateTimeOffset.UnixEpoch);
                existing.DynamicApi.GetList<string>("BeatmapMD5Hashes").Add("aaa");
            });
            return 0;
        });
        LazerRealmReader.DeleteCopy(path, keepMain: true);
        return path;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void Adds_new_collections_and_merges_into_ones_with_the_same_name()
    {
        using var dir = new TempDirectory();
        var realm = CreateRealm(dir.Path);
        var original = File.ReadAllBytes(realm);

        var result = LazerRealmWriter.AddCollections(realm, dir.File("work"), dir.File("backups"),
            [new("Favourites", ["aaa", "bbb"]), new("New one", ["ccc"])], Now);

        Assert.Equal((1, 1, 2), (result.Created, result.Updated, result.HashesAdded));
        Assert.Equal(original, File.ReadAllBytes(result.Backup));

        var content = LazerRealmReader.Read(realm, dir.File("read"));
        Assert.Equal((ulong?)52, content.SchemaVersion); // the version lazer had: nothing was upgraded
        Assert.Equal(["aaa", "bbb"], content.Collections.Single(c => c.Name == "Favourites").Hashes);
        Assert.Equal(["ccc"], content.Collections.Single(c => c.Name == "New one").Hashes);
        Assert.Equal(Now, content.Collections.Single(c => c.Name == "New one").LastModified);
        Assert.Empty(Directory.GetFiles(dir.Path, "*.beatmapharvest-new"));
    }

    [Fact]
    public void Saving_the_same_collections_again_changes_nothing_in_them()
    {
        using var dir = new TempDirectory();
        var realm = CreateRealm(dir.Path);
        LazerRealmWriter.AddCollections(realm, dir.File("work"), dir.File("backups"), [new("Favourites", ["bbb"])], Now);

        var again = LazerRealmWriter.AddCollections(realm, dir.File("work"), dir.File("backups"), [new("Favourites", ["aaa", "bbb"])], Now);

        Assert.Equal((0, 0, 0), (again.Created, again.Updated, again.HashesAdded));
        Assert.Equal(["aaa", "bbb"], LazerRealmReader.Read(realm, dir.File("read")).Collections.Single().Hashes);
    }

    [Fact]
    public void Refuses_a_collection_shape_it_does_not_know_and_leaves_the_file_alone()
    {
        using var dir = new TempDirectory();
        var realm = CreateRealm(dir.Path, extraRequiredProperty: true);
        var before = Hash(realm);

        var error = Assert.Throws<InvalidOperationException>(() =>
            LazerRealmWriter.AddCollections(realm, dir.File("work"), dir.File("backups"), [new("X", ["ccc"])], Now));

        Assert.Contains("stores collections differently", error.Message, StringComparison.Ordinal);
        Assert.Equal(before, Hash(realm));
        Assert.False(Directory.Exists(dir.File("backups")) && Directory.EnumerateFiles(dir.File("backups")).Any());
    }

    [Fact]
    public void Reports_lazers_version_format_and_compatibility()
    {
        using var dir = new TempDirectory();
        var content = LazerRealmReader.Read(CreateRealm(dir.Path), dir.File("read"));

        Assert.Equal((ulong?)52, content.SchemaVersion);
        Assert.NotNull(content.FileFormat);
        Assert.True(LazerRealmWriter.Compatibility(content, dir.File("work")).Compatible);
        Assert.False(LazerRealmWriter.Compatibility(content with { FileFormat = 1 }, dir.File("work")).Compatible);
    }
}
