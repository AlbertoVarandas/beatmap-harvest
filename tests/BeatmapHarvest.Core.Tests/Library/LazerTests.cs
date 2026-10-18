using System.Text;
using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Tests.Library;

public class LazerTests
{
    private const string OsuFile = """
        osu file format v14

        [General]
        AudioFilename: audio.mp3
        Mode: 3

        [Metadata]
        Title:Tokyo Teddy Bear
        Artist:Utsu-P
        Creator:puxtu
        Version:[4K] NM
        BeatmapID:736930
        BeatmapSetID:318207

        [HitObjects]
        Title:not metadata
        """;

    [Fact]
    public void Reads_ids_and_metadata_from_a_stored_osu_file()
    {
        using var dir = new TempDirectory();
        var path = dir.File("a1b2");
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(OsuFile)]);

        var map = LazerLibraryScanner.TryRead(path)!;

        Assert.Equal(318207, map.BeatmapsetId);
        Assert.Equal(736930, map.BeatmapId);
        Assert.Equal("mania", map.Mode);
        Assert.Equal("Tokyo Teddy Bear", map.Title);
        Assert.Equal("[4K] NM", map.Version);
        Assert.Equal(32, map.Md5.Length);
    }

    [Fact]
    public void Scans_the_store_and_reuses_the_cache()
    {
        using var dir = new TempDirectory();
        var files = Directory.CreateDirectory(Path.Combine(dir.Path, "files", "a", "a1")).FullName;
        File.WriteAllText(Path.Combine(files, "a1map"), OsuFile);
        File.WriteAllBytes(Path.Combine(files, "a1audio"), [0xFF, 0xFB, 0x90, 0x00]);
        var cache = dir.File("lazer-index.json");

        var first = LazerLibraryScanner.Scan(Path.Combine(dir.Path, "files"), cache);
        // Stored files never change (they are named by their hash), so a cached answer is trusted.
        File.WriteAllText(Path.Combine(files, "a1map"), "garbage");
        var second = LazerLibraryScanner.Scan(Path.Combine(dir.Path, "files"), cache);

        Assert.Equal(318207, Assert.Single(first).BeatmapsetId);
        Assert.Equal(318207, Assert.Single(second).BeatmapsetId);

        File.Delete(Path.Combine(files, "a1map"));
        Assert.Empty(LazerLibraryScanner.Scan(Path.Combine(dir.Path, "files"), cache));
    }

    [Fact]
    public void Finds_lazer_data_and_follows_storage_ini()
    {
        using var dir = new TempDirectory();
        var moved = Directory.CreateDirectory(dir.File("moved")).FullName;
        File.WriteAllText(Path.Combine(moved, "client.realm"), "");
        File.WriteAllText(dir.File("storage.ini"), $"FullPath = {moved}\n");

        Assert.Equal(moved, LazerInstall.FollowStorageIni(dir.Path));
        Assert.Equal(moved, LazerInstall.Find(moved)!.DataDirectory);
        Assert.Null(LazerInstall.Find(dir.Path)); // no client.realm in the default folder itself
    }

    [Theory]
    [InlineData(true, true, null, OsuClient.Lazer)]
    [InlineData(true, true, OsuClient.Stable, OsuClient.Stable)]
    [InlineData(true, false, OsuClient.Lazer, OsuClient.Stable)]
    [InlineData(false, true, OsuClient.Stable, OsuClient.Lazer)]
    [InlineData(false, false, null, null)]
    public void Prefers_the_chosen_client_then_lazer(bool stable, bool lazer, OsuClient? preferred, OsuClient? expected)
    {
        var clients = new OsuClients(
            stable ? new OsuInstall("s", "s/Songs", null) : null,
            lazer ? new LazerInstall("l", null) : null);

        Assert.Equal(expected, clients.Resolve(preferred));
        Assert.Equal(stable && lazer, clients.BothFound);
    }
}
