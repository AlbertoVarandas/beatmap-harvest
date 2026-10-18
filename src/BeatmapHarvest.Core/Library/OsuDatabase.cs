namespace BeatmapHarvest.Core.Library;

/// <summary>One difficulty installed in osu! stable, as listed in <c>osu!.db</c>.</summary>
public sealed record LocalBeatmap(
    string Md5,
    long BeatmapId,
    long BeatmapsetId,
    string Artist,
    string Title,
    string Creator,
    string Version,
    string Mode,
    double? Stars,
    int DrainSeconds,
    string FolderName,
    string OsuFile = "");

public sealed record OsuDatabase(int Version, string PlayerName, IReadOnlyList<LocalBeatmap> Beatmaps)
{
    /// <summary>Online beatmapset ids of the installed maps (maps never uploaded have id 0 or -1 and are left out).</summary>
    public HashSet<long> BeatmapsetIds() => Beatmaps.Where(b => b.BeatmapsetId > 0).Select(b => b.BeatmapsetId).ToHashSet();

    public HashSet<string> Md5s() => Beatmaps.Select(b => b.Md5).Where(m => m.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Reads osu! stable's <c>osu!.db</c>. The layout changed over the years (entry-size prefix before 20191106,
/// byte vs float attributes before 20140609, star ratings as doubles before 2025 and floats after). The reader
/// tries the layout the version suggests first, then the others, and accepts the one that ends exactly at the
/// end of the file — so a wrong guess is never returned as data.
/// </summary>
public static class OsuDatabaseReader
{
    private static readonly string[] Modes = ["osu", "taiko", "fruits", "mania"];

    internal enum StarEncoding
    {
        None,
        Doubles,
        Floats,
    }

    internal sealed record Layout(bool EntrySizePrefix, bool FloatAttributes, StarEncoding Stars);

    /// <summary>Reads a copy of the file, so osu! can keep using it.</summary>
    public static OsuDatabase Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Read(bytes);
    }

    public static OsuDatabase Read(byte[] bytes)
    {
        if (bytes.Length < 8)
            throw new InvalidDataException("osu!.db is too short.");
        var version = BitConverter.ToInt32(bytes, 0);

        var errors = new List<string>();
        foreach (var layout in CandidateLayouts(version))
        {
            try
            {
                return Parse(bytes, layout);
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException or IOException)
            {
                errors.Add($"{layout}: {ex.Message}");
            }
        }
        throw new InvalidDataException($"Unrecognised osu!.db layout (version {version}). Tried: {string.Join(" | ", errors)}");
    }

    internal static IEnumerable<Layout> CandidateLayouts(int version)
    {
        var sizePrefix = version < 20191106;
        var floatAttributes = version >= 20140609;
        if (!floatAttributes)
        {
            yield return new Layout(sizePrefix, false, StarEncoding.None);
            yield break;
        }
        var preferred = version >= 20250107 ? StarEncoding.Floats : StarEncoding.Doubles;
        yield return new Layout(sizePrefix, true, preferred);
        yield return new Layout(sizePrefix, true, preferred == StarEncoding.Floats ? StarEncoding.Doubles : StarEncoding.Floats);
    }

    internal static OsuDatabase Parse(byte[] bytes, Layout layout)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes, writable: false));
        var version = reader.ReadInt32();
        reader.ReadInt32(); // folder count
        reader.ReadBoolean(); // account unlocked
        reader.ReadInt64(); // unlock date
        var player = reader.ReadOsuString();
        var count = reader.ReadInt32();
        if (count < 0 || count > bytes.Length / 40)
            throw new InvalidDataException($"Implausible beatmap count {count}.");

        var beatmaps = new List<LocalBeatmap>(count);
        for (var i = 0; i < count; i++)
            beatmaps.Add(ReadBeatmap(reader, layout));

        reader.ReadInt32(); // user permissions
        if (reader.BaseStream.Position != bytes.Length)
            throw new InvalidDataException($"Ended at {reader.BaseStream.Position} of {bytes.Length} bytes.");
        return new OsuDatabase(version, player, beatmaps);
    }

    private static LocalBeatmap ReadBeatmap(BinaryReader r, Layout layout)
    {
        if (layout.EntrySizePrefix)
            r.ReadInt32();
        var artist = r.ReadOsuString();
        r.ReadOsuString(); // artist unicode
        var title = r.ReadOsuString();
        r.ReadOsuString(); // title unicode
        var creator = r.ReadOsuString();
        var version = r.ReadOsuString();
        r.ReadOsuString(); // audio file
        var md5 = r.ReadOsuString();
        var osuFile = r.ReadOsuString();
        r.ReadByte(); // ranked status
        r.Skip(2 + 2 + 2 + 8); // circles, sliders, spinners, last modified
        r.Skip(layout.FloatAttributes ? 4 * 4 : 4); // AR, CS, HP, OD
        r.ReadDouble(); // slider velocity

        var stars = new double?[4];
        if (layout.Stars != StarEncoding.None)
        {
            for (var ruleset = 0; ruleset < 4; ruleset++)
                stars[ruleset] = ReadStarRatings(r, layout.Stars);
        }

        var drain = r.ReadInt32();
        r.ReadInt32(); // total time (ms)
        r.ReadInt32(); // preview time
        var timingPoints = r.ReadInt32();
        if (timingPoints < 0 || timingPoints > 100_000)
            throw new InvalidDataException($"Implausible timing point count {timingPoints}.");
        r.Skip(timingPoints * 17); // double BPM, double offset, bool inherited
        var beatmapId = r.ReadInt32();
        var beatmapsetId = r.ReadInt32();
        r.ReadInt32(); // thread id
        r.Skip(4); // grades per mode
        r.ReadInt16(); // local offset
        r.ReadSingle(); // stack leniency
        var mode = r.ReadByte();
        if (mode > 3)
            throw new InvalidDataException($"Bad gameplay mode {mode}.");
        r.ReadOsuString(); // source
        r.ReadOsuString(); // tags
        r.ReadInt16(); // online offset
        r.ReadOsuString(); // title font
        r.ReadBoolean(); // unplayed
        r.ReadInt64(); // last played
        r.ReadBoolean(); // osz2
        var folder = r.ReadOsuString();
        r.ReadInt64(); // last online check
        r.Skip(5); // ignore sound, ignore skin, disable storyboard, disable video, visual override
        if (!layout.FloatAttributes)
            r.ReadInt16(); // unknown, old versions only
        r.ReadInt32(); // last modification time
        r.ReadByte(); // mania scroll speed

        return new LocalBeatmap(md5, beatmapId, beatmapsetId, artist, title, creator, version, Modes[mode],
            stars[mode], drain, folder, osuFile);
    }

    /// <summary>Star ratings per mod combination; returns the no-mod one.</summary>
    private static double? ReadStarRatings(BinaryReader r, StarEncoding encoding)
    {
        var pairs = r.ReadInt32();
        if (pairs < 0 || pairs > 10_000)
            throw new InvalidDataException($"Implausible star rating count {pairs}.");
        double? nomod = null;
        for (var i = 0; i < pairs; i++)
        {
            ExpectMarker(r, 0x08);
            var mods = r.ReadInt32();
            double value;
            if (encoding == StarEncoding.Doubles)
            {
                ExpectMarker(r, 0x0d);
                value = r.ReadDouble();
            }
            else
            {
                ExpectMarker(r, 0x0c);
                value = r.ReadSingle();
            }
            // osu! stores a negative value to mark a rating it will recalculate; the magnitude is still the rating.
            if (mods == 0)
                nomod = Math.Abs(value);
        }
        return nomod;
    }

    private static void ExpectMarker(BinaryReader r, byte expected)
    {
        var b = r.ReadByte();
        if (b != expected)
            throw new InvalidDataException($"Expected 0x{expected:x2}, found 0x{b:x2} at {r.BaseStream.Position - 1}.");
    }
}
