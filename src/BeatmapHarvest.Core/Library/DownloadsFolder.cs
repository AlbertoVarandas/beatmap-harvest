namespace BeatmapHarvest.Core.Library;

/// <param name="Name">The file name without the leading beatmapset id and the extension ("Artist - Title").</param>
/// <param name="InGame">The beatmapset is already in osu!.</param>
public sealed record DownloadedArchive(string Path, long? SetId, string Name, long Size, DateTime Modified, bool InGame);

/// <summary>
/// The .osz files waiting in the downloads folder. Unlike a download run, which only lives while the app is open,
/// the folder keeps maps from earlier runs until they are imported or moved.
/// </summary>
public static class DownloadsFolder
{
    /// <summary>Newest first. Unfinished <c>.part</c> downloads are left out.</summary>
    public static IReadOnlyList<DownloadedArchive> List(string directory, IReadOnlySet<long> inGame)
    {
        if (!Directory.Exists(directory))
            return [];
        return new DirectoryInfo(directory).EnumerateFiles("*.osz")
            .Select(file =>
            {
                var id = OwnedMapsetScanner.ParseSetId(file.Name);
                return new DownloadedArchive(file.FullName, id, DisplayName(file.Name, id), file.Length, file.LastWriteTime,
                    id is { } setId && inGame.Contains(setId));
            })
            .OrderByDescending(a => a.Modified)
            .ToList();
    }

    private static string DisplayName(string fileName, long? id)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (id is null)
            return name;
        var rest = name[id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Length..].TrimStart(' ', '_', '.', '-');
        return rest.Length > 0 ? rest : name;
    }
}
