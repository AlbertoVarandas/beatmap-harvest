using System.Text;
using System.Text.Json;
using BeatmapHarvest.Core.Json;

namespace BeatmapHarvest.Core.Storage;

/// <summary>
/// Writes files so that a crash never leaves a half-written file behind:
/// write to <c>.tmp</c>, flush to disk, then rename over the target.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllBytes(string path, ReadOnlySpan<byte> bytes)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temp, path, overwrite: true);
    }

    public static void WriteAllText(string path, string text) => WriteAllBytes(path, Encoding.UTF8.GetBytes(text));

    public static void WriteJson<T>(string path, T value) =>
        WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Files));

    /// <summary>
    /// Reads a JSON file. Missing → <paramref name="fallback"/>.
    /// Unreadable JSON is renamed to <c>.corrupt</c> (kept for inspection) and <paramref name="fallback"/> is returned.
    /// </summary>
    public static T ReadJsonOrDefault<T>(string path, T fallback)
    {
        if (!File.Exists(path))
            return fallback;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), JsonDefaults.Files) ?? fallback;
        }
        catch (JsonException)
        {
            File.Move(path, path + ".corrupt", overwrite: true);
            return fallback;
        }
    }
}
