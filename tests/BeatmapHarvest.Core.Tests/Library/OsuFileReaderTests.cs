using BeatmapHarvest.Core.Library;

namespace BeatmapHarvest.Core.Tests.Library;

public class OsuFileReaderTests
{
    [Fact]
    public void Finds_the_background_in_the_events_section()
    {
        string[] lines =
        [
            "osu file format v14", "[General]", "AudioFilename: a.mp3", "",
            "[Events]", "//Background and Video events", "Video,0,\"intro.mp4\"", "0,0,\"dwaawdw.jpg\",0,0", "//Break Periods",
            "[TimingPoints]",
        ];

        Assert.Equal("dwaawdw.jpg", OsuFileReader.ReadBackground(lines));
    }

    [Fact]
    public void Returns_null_without_a_background()
    {
        Assert.Null(OsuFileReader.ReadBackground(["[Events]", "//nothing", "[TimingPoints]", "0,0,\"not-events.jpg\""]));
        Assert.Null(OsuFileReader.ReadBackground(Path.Combine(Path.GetTempPath(), "does-not-exist.osu")));
    }
}
