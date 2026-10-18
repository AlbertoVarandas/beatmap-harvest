namespace BeatmapHarvest.Core.Downloads;

/// <summary>Moves downloaded <c>.osz</c> files into osu! stable's Songs folder, which imports them on the next start.</summary>
public static class SongsFolder
{
    public sealed record MoveResult(int Moved, int AlreadyThere);

    public static MoveResult MoveArchives(string downloadsDirectory, string songsDirectory)
    {
        if (!Directory.Exists(downloadsDirectory))
            return new MoveResult(0, 0);
        Directory.CreateDirectory(songsDirectory);

        int moved = 0, existing = 0;
        foreach (var file in Directory.EnumerateFiles(downloadsDirectory, "*.osz"))
        {
            var target = Path.Combine(songsDirectory, Path.GetFileName(file));
            if (File.Exists(target))
            {
                existing++;
                continue;
            }
            File.Move(file, target);
            moved++;
        }
        return new MoveResult(moved, existing);
    }
}
