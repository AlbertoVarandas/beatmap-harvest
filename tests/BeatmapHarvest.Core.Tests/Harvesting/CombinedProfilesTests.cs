using BeatmapHarvest.Core.Harvesting;
using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests.Harvesting;

public class CombinedProfilesTests
{
    private static DifficultyInfo D(long id, int? plays) => new() { Id = id, Plays = plays };

    private static MapsetInfo Set(long id, string source, params DifficultyInfo[] diffs) => new()
    {
        Id = id,
        Title = $"set {id}",
        Sources = [source],
        Difficulties = diffs,
    };

    private static HarvestResult Profile(long userId, params MapsetInfo[] sets) => new() { UserId = userId, Mapsets = sets };

    private static readonly CombinedProfiles Sample = CombinedProfiles.From(
    [
        Profile(1, Set(10, HarvestSource.Best, D(100, null)), Set(20, HarvestSource.MostPlayed, D(200, 5))),
        Profile(2, Set(20, HarvestSource.MostPlayed, D(200, 3), D(201, null)), Set(30, HarvestSource.Best, D(300, null))),
        Profile(3, Set(20, HarvestSource.Firsts, D(200, null)), Set(10, HarvestSource.Recent, D(100, null))),
    ]);

    [Fact]
    public void Lists_each_map_once_and_adds_up_plays()
    {
        Assert.Equal([10L, 20L, 30L], Sample.Mapsets.Select(m => m.Id));

        var shared = Sample.Mapsets.Single(m => m.Id == 20);
        Assert.Equal([HarvestSource.MostPlayed, HarvestSource.Firsts], shared.Sources);
        Assert.Equal(8, shared.Difficulties.Single(d => d.Id == 200).Plays);
        Assert.Null(shared.Difficulties.Single(d => d.Id == 201).Plays);
        Assert.Equal([1L, 2L, 3L], Sample.Owners[20].Order());
    }

    [Fact]
    public void In_common_means_in_every_chosen_profile_or_in_two_or_more()
    {
        // No profile chosen: found in at least two.
        Assert.Equal([10L, 20L], Sample.Mapsets.Where(m => Sample.InCommon(m.Id, [])).Select(m => m.Id));
        // One chosen: shared between it and another profile.
        Assert.Equal([20L], Sample.Mapsets.Where(m => Sample.InCommon(m.Id, [2])).Select(m => m.Id));
        // Several chosen: in all of them.
        Assert.Equal([10L, 20L], Sample.Mapsets.Where(m => Sample.InCommon(m.Id, [1, 3])).Select(m => m.Id));
        Assert.Equal([20L], Sample.Mapsets.Where(m => Sample.InCommon(m.Id, [1, 2, 3])).Select(m => m.Id));
    }

    [Fact]
    public void In_any_keeps_maps_of_the_chosen_profiles()
    {
        Assert.Equal([10L, 20L, 30L], Sample.Mapsets.Where(m => Sample.InAny(m.Id, [])).Select(m => m.Id));
        Assert.Equal([20L, 30L], Sample.Mapsets.Where(m => Sample.InAny(m.Id, [2])).Select(m => m.Id));
    }
}
