using BeatmapHarvest.Core.Library;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.Core.Tests.Library;

public class OwnedMapsetScannerTests
{
    [Theory]
    [InlineData("400078 Artist - Title.osz", 400078L)]
    [InlineData("400078", 400078L)]
    [InlineData("400078_x", 400078L)]
    [InlineData("400078.osz", 400078L)]
    [InlineData("Artist - 400078", null)]
    [InlineData("400078abc", null)]
    [InlineData("0 zero", null)]
    public void Parses_leading_ids(string name, long? expected)
    {
        Assert.Equal(expected, OwnedMapsetScanner.ParseSetId(name));
    }

    [Fact]
    public void Scans_osz_files_and_folders_but_not_part_files()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("1 A - B.osz"), "");
        File.WriteAllText(dir.File("2 A - B.osz.part"), "");
        File.WriteAllText(dir.File("notes.txt"), "");
        Directory.CreateDirectory(dir.File("3 Folder"));

        Assert.Equal([1L], OwnedMapsetScanner.Scan(dir.Path, includeDirectories: false));
        Assert.Equal([1L, 3L], OwnedMapsetScanner.Scan(dir.Path, includeDirectories: true).Order());
        Assert.Empty(OwnedMapsetScanner.Scan(dir.File("missing"), includeDirectories: true));
    }
}

public class DownloadLedgerTests
{
    [Fact]
    public void Persists_marked_ids()
    {
        using var dir = new TempDirectory();
        var path = dir.File("done.json");

        new DownloadLedger(path).MarkDone(42);

        Assert.True(new DownloadLedger(path).Contains(42));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Renames_a_corrupt_file_and_starts_empty()
    {
        using var dir = new TempDirectory();
        var path = dir.File("done.json");
        File.WriteAllText(path, "{not json");

        var ledger = new DownloadLedger(path);

        Assert.Empty(ledger.Snapshot());
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public void Collects_owned_ids_from_every_source()
    {
        using var dir = new TempDirectory();
        var ledger = new DownloadLedger(dir.File("done.json"));
        ledger.MarkDone(1);
        Directory.CreateDirectory(dir.File("downloads"));
        File.WriteAllText(Path.Combine(dir.File("downloads"), "2 x.osz"), "");
        Directory.CreateDirectory(Path.Combine(dir.File("Songs"), "3 y"));

        var owned = OwnedMapsets.Collect(ledger, dir.File("downloads"), dir.File("Songs"));

        Assert.Equal([1L, 2L, 3L], owned.Order());
    }
}

public class AtomicFileTests
{
    [Fact]
    public void Round_trips_json_and_creates_directories()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "a", "b", "settings.json");

        AtomicFile.WriteJson(path, new AppSettings { MaxParallelDownloads = 4, LastProfile = "Ãrtist ☆" });
        var loaded = AtomicFile.ReadJsonOrDefault(path, new AppSettings());

        Assert.Equal(4, loaded.MaxParallelDownloads);
        Assert.Equal("Ãrtist ☆", loaded.LastProfile);
        Assert.Contains("max_parallel_downloads", File.ReadAllText(path), StringComparison.Ordinal);
    }
}
