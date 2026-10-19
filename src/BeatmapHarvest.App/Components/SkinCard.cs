namespace BeatmapHarvest.App.Components;

/// <summary>A skin as the Skins page shows it, whichever osu! it comes from.</summary>
/// <param name="Author">Already formatted ("by …" or "unknown author").</param>
/// <param name="Detail">File count, skin version, or "Comes with osu!lazer.".</param>
/// <param name="Previews">Translated element name and image URL.</param>
/// <param name="Folder">The skin's folder (osu! stable only).</param>
public sealed record SkinCard(
    string Name,
    string Author,
    string Detail,
    bool Current,
    bool BuiltIn,
    IReadOnlyList<(string Label, string Url)> Previews,
    string? Folder);
