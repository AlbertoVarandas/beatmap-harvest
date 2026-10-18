using System.Text;

namespace BeatmapHarvest.Core.Library;

/// <summary>Reads and writes the primitive types of osu! stable's .db files (little-endian; strings as 0x00 or 0x0b + ULEB128 length + UTF-8).</summary>
public static class OsuBinary
{
    public static string ReadOsuString(this BinaryReader reader)
    {
        var marker = reader.ReadByte();
        if (marker == 0x00)
            return "";
        if (marker != 0x0b)
            throw new InvalidDataException($"Bad string marker 0x{marker:x2} at {reader.BaseStream.Position - 1}.");
        var length = (int)ReadUleb128(reader);
        if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("String length runs past the end of the file.");
        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }

    public static void WriteOsuString(this BinaryWriter writer, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            writer.Write((byte)0x00);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((byte)0x0b);
        var length = (uint)bytes.Length;
        do
        {
            var b = (byte)(length & 0x7f);
            length >>= 7;
            if (length != 0)
                b |= 0x80;
            writer.Write(b);
        }
        while (length != 0);
        writer.Write(bytes);
    }

    private static uint ReadUleb128(BinaryReader reader)
    {
        uint result = 0;
        var shift = 0;
        while (true)
        {
            var b = reader.ReadByte();
            result |= (uint)(b & 0x7f) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
            if (shift > 28)
                throw new InvalidDataException("ULEB128 value too long.");
        }
    }

    public static void Skip(this BinaryReader reader, int bytes) => reader.BaseStream.Seek(bytes, SeekOrigin.Current);
}
