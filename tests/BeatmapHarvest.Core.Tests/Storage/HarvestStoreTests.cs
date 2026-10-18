using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.Core.Tests.Storage;

public class HarvestStoreTests
{
    [Fact]
    public void Saves_lists_newest_first_and_deletes_profiles()
    {
        using var dir = new TempDirectory();
        var store = new HarvestStore(new AppPaths(dir.Path));
        store.Save(new HarvestResult { UserId = 1, Username = "old", HarvestedAt = DateTimeOffset.UnixEpoch, Mapsets = [Samples.Mapset(10, Samples.Diff())] });
        store.Save(new HarvestResult { UserId = 2, Username = "new", HarvestedAt = DateTimeOffset.UnixEpoch.AddDays(1) });

        Assert.Equal(["new", "old"], store.LoadAll().Select(r => r.Username));
        Assert.Equal(10, Assert.Single(store.Load(1)!.Mapsets).Id);

        store.Delete(1);

        Assert.Null(store.Load(1));
        Assert.Equal(["new"], store.LoadAll().Select(r => r.Username));
        store.Delete(99); // unknown profile: nothing to do
    }
}
