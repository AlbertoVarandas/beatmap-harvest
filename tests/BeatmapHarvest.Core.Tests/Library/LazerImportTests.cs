using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Tests.Library;

public class LazerImportTests
{
    [Fact]
    public void Splits_by_count()
    {
        var files = Enumerable.Range(1, 120).Select(i => $@"C:\d\{i} Song.osz").ToList();

        var batches = LazerImport.Batches(files, batchSize: 50);

        Assert.Equal([50, 50, 20], batches.Select(b => b.Count));
        Assert.Equal(files, batches.SelectMany(b => b));
    }

    [Fact]
    public void Splits_by_command_line_length()
    {
        var longName = new string('x', 900);
        var files = Enumerable.Range(1, 10).Select(i => $@"C:\{i}{longName}.osz").ToList();

        var batches = LazerImport.Batches(files, batchSize: 50, maxChars: 3_000);

        Assert.All(batches, b => Assert.True(b.Sum(f => f.Length + 3) <= 3_000));
        Assert.Equal(10, batches.Sum(b => b.Count));
        Assert.Empty(LazerImport.Batches([]));
    }

    [Fact]
    public void A_single_file_longer_than_the_limit_still_gets_its_own_batch()
    {
        var huge = new string('y', 500) + ".osz";

        var batches = LazerImport.Batches(["a.osz", huge, "b.osz"], maxChars: 100);

        Assert.Equal(3, batches.Count);
    }

    [Fact]
    public void Pending_archives_skip_sets_already_in_the_game()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("100 A - B.osz"), "");
        File.WriteAllText(dir.File("200 C - D.osz"), "");
        File.WriteAllText(dir.File("no id.osz"), "");
        File.WriteAllText(dir.File("300 E - F.osz.part"), "");

        var pending = LazerImport.PendingArchives(dir.Path, new HashSet<long> { 100 });

        Assert.Equal(["200 C - D.osz", "no id.osz"], pending.Select(Path.GetFileName));
        Assert.Empty(LazerImport.PendingArchives(dir.File("missing"), new HashSet<long>()));
    }

    [Fact]
    public void Lists_the_downloads_folder_newest_first_with_what_is_in_the_game()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("100 A - B.osz"), "x");
        File.SetLastWriteTime(dir.File("100 A - B.osz"), new DateTime(2026, 1, 1));
        File.WriteAllText(dir.File("200 C - D.osz"), "xy");
        File.SetLastWriteTime(dir.File("200 C - D.osz"), new DateTime(2026, 2, 1));
        File.WriteAllText(dir.File("no id.osz"), "");
        File.SetLastWriteTime(dir.File("no id.osz"), new DateTime(2025, 1, 1));
        File.WriteAllText(dir.File("300 E - F.osz.part"), "");

        var list = DownloadsFolder.List(dir.Path, new HashSet<long> { 100 });

        Assert.Equal(["C - D", "A - B", "no id"], list.Select(a => a.Name));
        Assert.Equal([false, true, false], list.Select(a => a.InGame));
        Assert.Equal([200L, 100L, null], list.Select(a => a.SetId));
        Assert.Equal(2, list[0].Size);
        Assert.Empty(DownloadsFolder.List(dir.File("missing"), new HashSet<long>()));
    }
}
