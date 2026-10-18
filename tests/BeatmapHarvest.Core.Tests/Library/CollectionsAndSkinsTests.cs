using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Tests.Library;

public class CollectionDatabaseTests
{
    [Fact]
    public void Reads_an_empty_file_written_by_osu()
    {
        // The exact bytes of a fresh collection.db: version 20240330, no collections.
        var db = CollectionDatabase.Read([0xca, 0xd7, 0x34, 0x01, 0, 0, 0, 0]);

        Assert.Equal(20240330, db.Version);
        Assert.Empty(db.Collections);
        Assert.Equal([0xca, 0xd7, 0x34, 0x01, 0, 0, 0, 0], db.ToBytes());
    }

    [Fact]
    public void Round_trips_collections_with_unicode_names()
    {
        var db = new CollectionDatabase();
        db.Collections.Add(new OsuCollection("Mania ★ favourites", ["aaa", "bbb"]));
        db.Collections.Add(new OsuCollection("empty"));

        var read = CollectionDatabase.Read(db.ToBytes());

        Assert.Equal(["Mania ★ favourites", "empty"], read.Collections.Select(c => c.Name));
        Assert.Equal(["aaa", "bbb"], read.Collections[0].Hashes);
        Assert.Empty(read.Collections[1].Hashes);
    }

    [Fact]
    public void Add_skips_duplicates_ignoring_case()
    {
        var collection = new OsuCollection("x", ["ABC"]);

        Assert.Equal(1, collection.Add(["abc", "def", "", "DEF"]));
        Assert.Equal(["ABC", "def"], collection.Hashes);
    }

    [Fact]
    public void Save_backs_up_the_previous_file()
    {
        using var dir = new TempDirectory();
        var path = dir.File("collection.db");
        File.WriteAllBytes(path, [0xca, 0xd7, 0x34, 0x01, 0, 0, 0, 0]);
        var db = CollectionDatabase.Read(path);
        db.Collections.Add(new OsuCollection("new", ["h1"]));

        var backup = db.Save(path, new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

        Assert.NotNull(backup);
        Assert.Equal(8, new FileInfo(backup).Length);
        Assert.Equal("new", Assert.Single(CollectionDatabase.Read(path).Collections).Name);
        Assert.Null(new CollectionDatabase().Save(dir.File("fresh.db"), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rejects_trailing_garbage()
    {
        Assert.Throws<InvalidDataException>(() => CollectionDatabase.Read([0xca, 0xd7, 0x34, 0x01, 0, 0, 0, 0, 1]));
    }
}

public class SkinCatalogTests
{
    [Fact]
    public void Lists_skins_with_ini_metadata_and_previews()
    {
        using var dir = new TempDirectory();
        var skin = Directory.CreateDirectory(dir.File("Cool Skin")).FullName;
        File.WriteAllText(Path.Combine(skin, "skin.ini"), "[General]\nName: Cool Skin v2\nAuthor: someone\n// comment: no\n[Colours]\nName: wrong\n");
        File.WriteAllText(Path.Combine(skin, "cursor.png"), "");
        File.WriteAllText(Path.Combine(skin, "cursor@2x.png"), "");
        File.WriteAllText(Path.Combine(skin, "hitcircle.png"), "");
        Directory.CreateDirectory(dir.File("Bare"));

        var skins = SkinCatalog.List(dir.Path);

        Assert.Equal(["Bare", "Cool Skin v2"], skins.Select(s => s.Name));
        var cool = skins[1];
        Assert.Equal("someone", cool.Author);
        Assert.Equal("Cool Skin", cool.Folder);
        Assert.Equal([Path.Combine("Cool Skin", "cursor@2x.png"), Path.Combine("Cool Skin", "hitcircle.png")], cool.Previews.Select(p => p.RelativePath));
        Assert.Empty(skins[0].Previews);
        Assert.Empty(SkinCatalog.List(dir.File("missing")));
    }
}
