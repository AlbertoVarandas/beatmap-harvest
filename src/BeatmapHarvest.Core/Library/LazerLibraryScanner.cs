using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.Core.Library;

/// <summary>
/// Lists the difficulties installed in osu!lazer without opening its Realm database: every <c>.osu</c> file in the
/// <c>files</c> store carries its beatmap and beatmapset ids and metadata. Files are named by the hash of their content,
/// so what was learned about a file never changes; it is cached and only new files are read on later scans.
/// The store may still hold files of maps deleted in lazer until lazer cleans it up.
/// </summary>
public static class LazerLibraryScanner
{
    private static readonly byte[] Signature = "osu file format"u8.ToArray();

    /// <summary>A cached result for one stored file; null fields mean "not a .osu file".</summary>
    internal sealed record CachedFile(LocalBeatmap? Beatmap);

    public static IReadOnlyList<LocalBeatmap> Scan(string filesDirectory, string cacheFile)
    {
        if (!Directory.Exists(filesDirectory))
            return [];

        var cache = AtomicFile.ReadJsonOrDefault(cacheFile, new Dictionary<string, CachedFile>(StringComparer.Ordinal));
        var seen = new Dictionary<string, CachedFile>(StringComparer.Ordinal);
        var changed = false;

        foreach (var path in Directory.EnumerateFiles(filesDirectory, "*", SearchOption.AllDirectories))
        {
            var hash = Path.GetFileName(path);
            if (!cache.TryGetValue(hash, out var entry))
            {
                entry = new CachedFile(TryRead(path));
                changed = true;
            }
            seen[hash] = entry;
        }

        if (changed || seen.Count != cache.Count)
            AtomicFile.WriteJson(cacheFile, seen);

        return seen.Values.Select(e => e.Beatmap).OfType<LocalBeatmap>().ToList();
    }

    /// <summary>The difficulty described by a stored file, or null when the file is not a .osu file.</summary>
    internal static LocalBeatmap? TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var head = new byte[Signature.Length + 3];
            var read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            var start = read >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF ? 3 : 0;
            if (read - start < Signature.Length || !head.AsSpan(start, Signature.Length).SequenceEqual(Signature))
                return null;

            var bytes = File.ReadAllBytes(path);
            // osu! identifies difficulties by the MD5 of the .osu file; this is an identifier, not security.
#pragma warning disable CA5351
            var md5 = Convert.ToHexStringLower(MD5.HashData(bytes));
#pragma warning restore CA5351
            return Parse(Encoding.UTF8.GetString(bytes).Split('\n'), md5);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static LocalBeatmap Parse(IEnumerable<string> lines, string md5)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "";
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                section = line;
                // Everything needed is in [General] and [Metadata], near the top.
                if (section is "[TimingPoints]" or "[HitObjects]")
                    break;
                continue;
            }
            if (section is not ("[General]" or "[Metadata]"))
                continue;
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
                values.TryAdd(line[..colon].Trim(), line[(colon + 1)..].Trim());
        }

        var mode = int.TryParse(values.GetValueOrDefault("Mode"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m is >= 0 and <= 3 ? m : 0;
        return new LocalBeatmap(
            md5,
            Id(values, "BeatmapID"),
            Id(values, "BeatmapSetID"),
            values.GetValueOrDefault("Artist") ?? "",
            values.GetValueOrDefault("Title") ?? "",
            values.GetValueOrDefault("Creator") ?? "",
            values.GetValueOrDefault("Version") ?? "",
            Osu.Ruleset.All[mode],
            Stars: null,
            DrainSeconds: 0,
            FolderName: "");
    }

    private static long Id(Dictionary<string, string> values, string key) =>
        long.TryParse(values.GetValueOrDefault(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : -1;
}
