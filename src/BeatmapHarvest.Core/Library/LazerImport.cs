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

    /// <summary>
    /// Waits until osu!lazer has imported <paramref name="files"/>: it deletes each .osz once imported, so the import
    /// is over when they are gone. Gives up when none disappeared for <paramref name="stall"/> (a map lazer refused
    /// stays). Reports how many are done after every check; returns how many are left.
    /// </summary>
    public static async Task<int> WaitForImportAsync(IReadOnlyList<string> files, TimeSpan stall, TimeSpan poll,
        IProgress<int>? imported, CancellationToken cancellationToken, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        var left = files.Count(exists);
        var lastChange = DateTime.UtcNow;
        imported?.Report(files.Count - left);
        while (left > 0 && DateTime.UtcNow - lastChange < stall)
        {
            await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            var now = files.Count(exists);
            if (now != left)
            {
                left = now;
                lastChange = DateTime.UtcNow;
                imported?.Report(files.Count - left);
            }
        }
        return left;
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
