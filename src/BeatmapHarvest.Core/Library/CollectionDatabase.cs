using System.Globalization;
using BeatmapHarvest.Core.Storage;

namespace BeatmapHarvest.Core.Library;

/// <summary>An osu! stable collection: a name and the MD5 hashes of its difficulties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "osu! calls these collections.")]
public sealed class OsuCollection(string name, IEnumerable<string>? hashes = null)
{
    public string Name { get; set; } = name;

    /// <summary>Difficulty MD5s, in order, without duplicates (compared case-insensitively).</summary>
    public List<string> Hashes { get; } = (hashes ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Adds hashes not already present; returns how many were added.</summary>
    public int Add(IEnumerable<string> hashes)
    {
        var existing = Hashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var hash in hashes)
        {
            if (hash.Length > 0 && existing.Add(hash))
            {
                Hashes.Add(hash);
                added++;
            }
        }
        return added;
    }
}

/// <summary>
/// osu! stable's <c>collection.db</c>: version, then each collection's name and difficulty hashes.
/// osu! rewrites this file when it exits, so it must be closed while the file is changed.
/// </summary>
public sealed class CollectionDatabase
{
    /// <summary>The version osu! writes when the file is new.</summary>
    public const int DefaultVersion = 20240330;

    public int Version { get; set; } = DefaultVersion;
    public List<OsuCollection> Collections { get; } = [];

    public static CollectionDatabase Read(string path) =>
        File.Exists(path) ? Read(File.ReadAllBytes(path)) : new CollectionDatabase();

    public static CollectionDatabase Read(byte[] bytes)
    {
        using var reader = new BinaryReader(new MemoryStream(bytes, writable: false));
        var db = new CollectionDatabase { Version = reader.ReadInt32() };
        var count = reader.ReadInt32();
        if (count < 0 || count > 100_000)
            throw new InvalidDataException($"Implausible collection count {count}.");
        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadOsuString();
            var size = reader.ReadInt32();
            if (size < 0 || size > 10_000_000)
                throw new InvalidDataException($"Implausible collection size {size}.");
            var hashes = new string[size];
            for (var h = 0; h < size; h++)
                hashes[h] = reader.ReadOsuString();
            db.Collections.Add(new OsuCollection(name, hashes));
        }
        if (reader.BaseStream.Position != bytes.Length)
            throw new InvalidDataException("collection.db has unexpected data at the end.");
        return db;
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Version);
        writer.Write(Collections.Count);
        foreach (var collection in Collections)
        {
            writer.WriteOsuString(collection.Name);
            writer.Write(collection.Hashes.Count);
            foreach (var hash in collection.Hashes)
                writer.WriteOsuString(hash);
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Writes the file atomically after copying the current one to <c>collection.db.&lt;timestamp&gt;.bak</c>.
    /// Returns the backup path (null when there was no file to back up).
    /// </summary>
    public string? Save(string path, DateTimeOffset now)
    {
        string? backup = null;
        if (File.Exists(path))
        {
            backup = $"{path}.{now.ToLocalTime().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.bak";
            File.Copy(path, backup, overwrite: true);
        }
        AtomicFile.WriteAllBytes(path, ToBytes());
        return backup;
    }
}
