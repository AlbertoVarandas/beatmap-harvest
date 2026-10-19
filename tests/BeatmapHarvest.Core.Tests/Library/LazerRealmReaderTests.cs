using BeatmapHarvest.Core.Library;
using Realms;
using Realms.Schema;

namespace BeatmapHarvest.Core.Tests.Library;

public class LazerRealmReaderTests
{
    /// <summary>The parts of osu!lazer's schema the reader uses (class and property names as lazer stores them).</summary>
    private static RealmSchema LazerLikeSchema() => new RealmSchema.Builder
    {
        new ObjectSchema.Builder("BeatmapCollection", ObjectSchema.ObjectType.RealmObject)
        {
            Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
            Property.Primitive("Name", RealmValueType.String, isNullable: true),
            Property.PrimitiveList("BeatmapMD5Hashes", RealmValueType.String, areElementsNullable: true),
            Property.Primitive("LastModified", RealmValueType.Date),
        },
        new ObjectSchema.Builder("File", ObjectSchema.ObjectType.RealmObject)
        {
            Property.Primitive("Hash", RealmValueType.String, isPrimaryKey: true, isNullable: true),
        },
        new ObjectSchema.Builder("RealmNamedFileUsage", ObjectSchema.ObjectType.EmbeddedObject)
        {
            Property.Object("File", "File"),
            Property.Primitive("Filename", RealmValueType.String, isNullable: true),
        },
        new ObjectSchema.Builder("Skin", ObjectSchema.ObjectType.RealmObject)
        {
            Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
            Property.Primitive("Name", RealmValueType.String, isNullable: true),
            Property.Primitive("Creator", RealmValueType.String, isNullable: true),
            Property.Primitive("Protected", RealmValueType.Bool),
            Property.ObjectList("Files", "RealmNamedFileUsage"),
            Property.Primitive("DeletePending", RealmValueType.Bool),
        },
    }.Build();

    private static string CreateLazerLikeRealm(string directory) =>
        LazerRealmReader.WithoutSynchronizationContext(() => WriteLazerLikeRealm(directory));

    private static string WriteLazerLikeRealm(string directory)
    {
        var path = Path.Combine(directory, "client.realm");
        var config = new RealmConfiguration(path) { Schema = LazerLikeSchema(), SchemaVersion = 52 };
        using var realm = Realm.GetInstance(config);
        realm.Write(() =>
        {
            var collection = realm.DynamicApi.CreateObject("BeatmapCollection", Guid.NewGuid());
            collection.DynamicApi.Set("Name", "Mania favourites");
            collection.DynamicApi.Set("LastModified", DateTimeOffset.UnixEpoch);
            var hashes = collection.DynamicApi.GetList<string>("BeatmapMD5Hashes");
            hashes.Add("aaa");
            hashes.Add("bbb");

            var skin = realm.DynamicApi.CreateObject("Skin", Guid.NewGuid());
            skin.DynamicApi.Set("Name", "My skin");
            skin.DynamicApi.Set("Creator", "me");
            var file = realm.DynamicApi.CreateObject("File", "abcdef123");
            var usage = realm.DynamicApi.AddEmbeddedObjectToList(skin.DynamicApi.GetList<IEmbeddedObject>("Files"));
            usage.DynamicApi.Set("File", RealmValue.Object(file));
            usage.DynamicApi.Set("Filename", "cursor@2x.png");

            var builtIn = realm.DynamicApi.CreateObject("Skin", Guid.NewGuid());
            builtIn.DynamicApi.Set("Name", "argon");
            builtIn.DynamicApi.Set("Protected", true);

            var deleted = realm.DynamicApi.CreateObject("Skin", Guid.NewGuid());
            deleted.DynamicApi.Set("Name", "gone");
            deleted.DynamicApi.Set("DeletePending", true);
        });
        return path;
    }

    [Fact]
    public void Reads_collections_and_skins_from_a_copy_and_leaves_the_original_alone()
    {
        using var dir = new TempDirectory();
        var original = CreateLazerLikeRealm(dir.Path);
        var hashBefore = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original)));
        var work = dir.File("work");

        var content = LazerRealmReader.Read(original, work);

        var collection = Assert.Single(content.Collections);
        Assert.Equal("Mania favourites", collection.Name);
        Assert.Equal(["aaa", "bbb"], collection.Hashes);

        Assert.Equal(["argon", "My skin"], content.Skins.Select(s => s.Name)); // built-in first, deleted left out
        Assert.True(content.Skins[0].Protected);
        var file = Assert.Single(content.Skins[1].Files);
        Assert.Equal(("cursor@2x.png", "abcdef123"), (file.Filename, file.Hash));

        Assert.Equal(hashBefore, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original))));
        Assert.Empty(Directory.GetFileSystemEntries(work));
    }

    [Fact]
    public void Lazer_skin_previews_point_into_the_store()
    {
        var skin = new LazerSkin(Guid.NewGuid(), "s", "", false,
            [new("Cursor.png", "aaa111"), new("cursor@2x.png", "bbb222"), new("skin.ini", "ccc333")]);

        var preview = Assert.Single(SkinCatalog.LazerPreviews(skin));

        Assert.Equal("Cursor", preview.Label);
        Assert.Equal(LazerRealmReader.StorePath("bbb222"), preview.RelativePath); // @2x first
    }

    [Fact]
    public void Store_path_follows_lazers_layout()
    {
        Assert.Equal(Path.Combine("a", "ab", "abcdef"), LazerRealmReader.StorePath("abcdef"));
    }
}
