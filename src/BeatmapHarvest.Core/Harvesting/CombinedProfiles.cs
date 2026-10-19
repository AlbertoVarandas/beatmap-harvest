namespace BeatmapHarvest.Core.Harvesting;

/// <summary>
/// The beatmapsets of several harvested profiles, each one once. A map found in several profiles keeps every source it
/// came from, and the play counts of its difficulties are added up.
/// </summary>
/// <param name="Owners">For each beatmapset id, the ids of the profiles it was found in.</param>
public sealed record CombinedProfiles(IReadOnlyList<MapsetInfo> Mapsets, IReadOnlyDictionary<long, IReadOnlySet<long>> Owners)
{
    public static CombinedProfiles From(IEnumerable<HarvestResult> profiles)
    {
        var sets = new Dictionary<long, MapsetInfo>();
        var order = new List<long>();
        var owners = new Dictionary<long, HashSet<long>>();
        foreach (var profile in profiles)
        {
            foreach (var mapset in profile.Mapsets)
            {
                if (!owners.TryGetValue(mapset.Id, out var users))
                    owners[mapset.Id] = users = [];
                users.Add(profile.UserId);

                if (sets.TryGetValue(mapset.Id, out var existing))
                {
                    sets[mapset.Id] = Merge(existing, mapset);
                }
                else
                {
                    sets[mapset.Id] = mapset;
                    order.Add(mapset.Id);
                }
            }
        }
        return new CombinedProfiles(
            order.Select(id => sets[id]).ToList(),
            owners.ToDictionary(o => o.Key, o => (IReadOnlySet<long>)o.Value));
    }

    /// <summary>
    /// Whether a beatmapset is shared: found in every one of <paramref name="profiles"/> when two or more are given;
    /// otherwise found in at least two profiles (including the given one, if any).
    /// </summary>
    public bool InCommon(long mapsetId, IReadOnlyCollection<long> profiles)
    {
        if (!Owners.TryGetValue(mapsetId, out var owners))
            return false;
        return profiles.Count >= 2
            ? profiles.All(owners.Contains)
            : owners.Count >= 2 && profiles.All(owners.Contains);
    }

    /// <summary>Found in at least one of <paramref name="profiles"/> (any profile when none is given).</summary>
    public bool InAny(long mapsetId, IReadOnlyCollection<long> profiles) =>
        profiles.Count == 0 || (Owners.TryGetValue(mapsetId, out var owners) && profiles.Any(owners.Contains));

    private static MapsetInfo Merge(MapsetInfo a, MapsetInfo b)
    {
        var difficulties = a.Difficulties.ToDictionary(d => d.Id);
        foreach (var d in b.Difficulties)
        {
            difficulties[d.Id] = difficulties.TryGetValue(d.Id, out var known)
                ? known with { Plays = known.Plays is null && d.Plays is null ? null : (known.Plays ?? 0) + (d.Plays ?? 0) }
                : d;
        }
        return a with
        {
            Sources = a.Sources.Union(b.Sources, StringComparer.Ordinal).ToList(),
            Difficulties = difficulties.Values.ToList(),
            Cover = a.Cover ?? b.Cover,
            Card = a.Card ?? b.Card,
            Preview = a.Preview ?? b.Preview,
        };
    }
}
