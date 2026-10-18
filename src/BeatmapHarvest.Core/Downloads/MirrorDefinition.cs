using System.Globalization;

namespace BeatmapHarvest.Core.Downloads;

/// <summary>A beatmap mirror. <see cref="UrlTemplate"/> contains <c>{id}</c> for the beatmapset id.</summary>
public sealed record MirrorDefinition(string Name, string UrlTemplate, bool Enabled = true)
{
    /// <summary>Tried in this order. Official downloads need a logged-in session, so mirrors are used instead.</summary>
    public static IReadOnlyList<MirrorDefinition> Defaults { get; } =
    [
        new("nekoha", "https://mirror.nekoha.moe/api/download/{id}"),
        new("catboy", "https://catboy.best/d/{id}"),
    ];

    public Uri BuildUri(long setId) =>
        new(UrlTemplate.Replace("{id}", setId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
}
