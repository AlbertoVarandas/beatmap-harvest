using BeatmapHarvest.Core.Osu;

namespace BeatmapHarvest.Core.Tests.Osu;

public class UserReferenceTests
{
    [Theory]
    [InlineData("https://osu.ppy.sh/users/123", "123", null)]
    [InlineData("https://osu.ppy.sh/users/123/mania", "123", "mania")]
    [InlineData("osu.ppy.sh/u/123/", "123", null)]
    [InlineData("https://osu.ppy.sh/users/peppy/osu?tab=scores", "@peppy", "osu")]
    [InlineData("https://osu.ppy.sh/users/123/notamode", "123", null)]
    [InlineData("  456  ", "456", null)]
    [InlineData("Some_Player", "@Some_Player", null)]
    [InlineData("@[Name]", "@[Name]", null)]
    public void Parses_profile_inputs(string input, string apiKey, string? mode)
    {
        Assert.True(UserReference.TryParse(input, out var reference));
        Assert.Equal(apiKey, reference.ApiKey);
        Assert.Equal(mode, reference.Mode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com/users/123")]
    [InlineData("https://osu.ppy.sh/beatmapsets/123")]
    [InlineData("name with way too many characters")]
    [InlineData("bad/name")]
    public void Rejects_invalid_inputs(string input)
    {
        Assert.False(UserReference.TryParse(input, out _));
    }
}
