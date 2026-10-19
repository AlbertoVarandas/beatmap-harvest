namespace BeatmapHarvest.Core.Library;

/// <summary>
/// Hands .osz files to osu!lazer the way the game supports: <c>osu!.exe a.osz b.osz …</c>. A running lazer receives them
/// through its IPC channel (the new process forwards each path and exits); a lazer that is not running imports them on start.
/// Beatmap Harvest never writes lazer's own database (client.realm).
/// </summary>
public static class LazerImport
{
    /// <summary>Windows limits a command line to 32 767 characters; stay well below it.</summary>
    public const int MaxCommandLineChars = 30_000;

    public const int DefaultBatchSize = 50;

    /// <summary>Splits the files into batches of at most <paramref name="batchSize"/> files that fit on one command line.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Batches(IEnumerable<string> files, int batchSize = DefaultBatchSize, int maxChars = MaxCommandLineChars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        var batches = new List<IReadOnlyList<string>>();
        var current = new List<string>();
        var length = 0;

        foreach (var file in files)
        {
            // Quotes plus a separating space.
            var cost = file.Length + 3;
            if (current.Count > 0 && (current.Count >= batchSize || length + cost > maxChars))
            {
                batches.Add(current);
                current = [];
                length = 0;
            }
            current.Add(file);
            length += cost;
        }
        if (current.Count > 0)
            batches.Add(current);
        return batches;
    }

    /// <summary>The .osz files in <paramref name="directory"/> whose beatmapset is not already in the game.</summary>
    public static IReadOnlyList<string> PendingArchives(string directory, IReadOnlySet<long> inGame)
    {
        if (!Directory.Exists(directory))
            return [];
        return Directory.EnumerateFiles(directory, "*.osz")
            .Where(f => OwnedMapsetScanner.ParseSetId(Path.GetFileName(f)) is not { } id || !inGame.Contains(id))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
