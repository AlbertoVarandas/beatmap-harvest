namespace BeatmapHarvest.Core.Library;

/// <summary>
/// The first 24 bytes of a Realm file: two top refs (16 bytes), the mnemonic "T-DB", two file-format bytes, a reserved
/// byte and flags whose lowest bit picks which of the two format bytes is current.
/// </summary>
internal static class RealmFileHeader
{
    /// <summary>The on-disk format of a Realm file; null when the file is not a Realm file or can't be read.</summary>
    public static int? FileFormat(string path)
    {
        try
        {
            // Shared, so it also works on lazer's own file while lazer has it open.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> header = stackalloc byte[24];
            if (stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length)
                return null;
            if (header[16] != 'T' || header[17] != '-' || header[18] != 'D' || header[19] != 'B')
                return null;
            return header[20 + (header[23] & 1)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
