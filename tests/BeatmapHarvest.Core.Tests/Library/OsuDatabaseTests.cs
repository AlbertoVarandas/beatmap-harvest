using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Selection;

namespace BeatmapHarvest.Core.Tests.Library;

public class OsuDatabaseTests
{
    /// <summary>Writes an osu!.db in a given layout, following the documented file structure.</summary>
    private static byte[] BuildDatabase(int version, bool floatStars, params (int SetId, int MapId, string Md5, byte Mode, double Stars)[] maps)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        var sizePrefix = version < 20191106;
        w.Write(version);
        w.Write(maps.Select(m => m.SetId).Distinct().Count());
        w.Write(true);
        w.Write(0L);
        w.WriteOsuString("Player");
        w.Write(maps.Length);
        foreach (var m in maps)
        {
            if (sizePrefix)
                w.Write(0);
            w.WriteOsuString("Artist");
            w.WriteOsuString("");
            w.WriteOsuString($"Title {m.SetId}");
            w.WriteOsuString("");
            w.WriteOsuString("Mapper");
            w.WriteOsuString($"Diff {m.MapId}");
            w.WriteOsuString("audio.mp3");
            w.WriteOsuString(m.Md5);
            w.WriteOsuString("x.osu");
            w.Write((byte)4);
            w.Write((short)10); w.Write((short)5); w.Write((short)1); w.Write(0L);
            w.Write(9f); w.Write(4f); w.Write(6f); w.Write(8f);
            w.Write(1.4);
            for (var mode = 0; mode < 4; mode++)
            {
                if (mode != m.Mode) { w.Write(0); continue; }
                w.Write(2);
                foreach (var (mods, stars) in new[] { (0, m.Stars), (64, m.Stars * 1.4) })
                {
                    w.Write((byte)0x08); w.Write(mods);
                    if (floatStars) { w.Write((byte)0x0c); w.Write((float)stars); }
                    else { w.Write((byte)0x0d); w.Write(stars); }
                }
            }
            w.Write(120); w.Write(125_000); w.Write(40_000);
            w.Write(1); w.Write(300.0); w.Write(0.0); w.Write(true);
            w.Write(m.MapId); w.Write(m.SetId); w.Write(0);
            w.Write(new byte[] { 9, 9, 9, 9 });
            w.Write((short)0); w.Write(0.7f); w.Write(m.Mode);
            w.WriteOsuString(""); w.WriteOsuString("tags"); w.Write((short)0); w.WriteOsuString("");
            w.Write(true); w.Write(0L); w.Write(false);
            w.WriteOsuString($"{m.SetId} Artist - Title");
            w.Write(0L);
            w.Write(new byte[5]);
            w.Write(0); w.Write((byte)0);
        }
        w.Write(0); // permissions
        return stream.ToArray();
    }

    [Theory]
    [InlineData(20260924, true)]   // current: float star ratings
    [InlineData(20250108, false)]  // doubles with a newer version number: fallback layout
    [InlineData(20210101, false)]  // doubles, no size prefix
    [InlineData(20180101, false)]  // doubles with the entry-size prefix
    public void Reads_every_star_rating_layout(int version, bool floatStars)
    {
        var bytes = BuildDatabase(version, floatStars,
            (100, 1000, "aaa", 3, 2.5), (100, 1001, "bbb", 3, 3.25), (200, 2000, "ccc", 0, 5));

        var db = OsuDatabaseReader.Read(bytes);

        Assert.Equal(version, db.Version);
        Assert.Equal("Player", db.PlayerName);
        Assert.Equal(3, db.Beatmaps.Count);
        Assert.Equal([100L, 200L], db.BeatmapsetIds().Order());
        Assert.Contains("BBB", db.Md5s());
        var mania = db.Beatmaps[1];
        Assert.Equal("mania", mania.Mode);
        Assert.Equal(3.25, mania.Stars!.Value, 3);
        Assert.Equal("100 Artist - Title", mania.FolderName);
        Assert.Equal("osu", db.Beatmaps[2].Mode);
    }

    [Fact]
    public void Rejects_a_truncated_file()
    {
        var bytes = BuildDatabase(20260924, true, (100, 1000, "aaa", 0, 2));
        Assert.Throws<InvalidDataException>(() => OsuDatabaseReader.Read(bytes[..^9]));
    }

    [Fact]
    public void Finds_songs_folder_and_skin_from_the_user_config()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("osu!.db"), "");
        File.WriteAllText(dir.File($"osu!.{Environment.UserName}.cfg"), "# comment\nBeatmapDirectory = D:\\Maps\nSkin = My Skin\n");

        var install = OsuInstall.Find(dir.Path)!;

        Assert.Equal(@"D:\Maps", install.SongsDirectory);
        Assert.Equal("My Skin", install.CurrentSkin);
        Assert.Equal(Path.Combine(dir.Path, "collection.db"), install.CollectionFile);
        Assert.Null(OsuInstall.Find(dir.File("missing")));
    }

    [Fact]
    public void Relative_songs_folder_is_inside_the_install()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("osu!.exe"), "");
        File.WriteAllText(dir.File("osu!.someone.cfg"), "BeatmapDirectory = Songs\n");

        Assert.Equal(Path.Combine(dir.Path, "Songs"), OsuInstall.Find(dir.Path)!.SongsDirectory);
    }

    [Fact]
    public void Ownership_combines_game_and_downloads()
    {
        using var dir = new TempDirectory();
        var ledger = new DownloadLedger(dir.File("done.json"));
        ledger.MarkDone(300);
        var db = OsuDatabaseReader.Read(BuildDatabase(20260924, true, (100, 1000, "aaa", 0, 2)));
        Directory.CreateDirectory(Path.Combine(dir.File("Songs"), "200 Folder"));

        var index = OwnedMapsets.Collect(ledger, dir.File("downloads"), dir.File("Songs"), db);

        Assert.Equal(MapOwnership.InGame, index.Of(100));
        Assert.Equal(MapOwnership.InGame, index.Of(200));
        Assert.Equal(MapOwnership.Downloaded, index.Of(300));
        Assert.Equal(MapOwnership.None, index.Of(400));
    }

    [Fact]
    public void Library_filter_overrides_skip_owned()
    {
        var mapset = Samples.Mapset(1, Samples.Diff());
        var filters = new SelectionFilters { SkipOwned = true };

        Assert.False(SelectionEngine.Matches(mapset, filters, MapOwnership.InGame));
        Assert.False(SelectionEngine.Matches(mapset, filters, MapOwnership.Downloaded));

        filters.Library.Add(MapOwnership.InGame);
        Assert.True(SelectionEngine.Matches(mapset, filters, MapOwnership.InGame));
        Assert.False(SelectionEngine.Matches(mapset, filters, MapOwnership.None));
    }
}
