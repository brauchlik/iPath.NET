using System.Text;

namespace iPath.Application.Features.Conversion.Dzi;

/// <summary>
/// Where the DZI descriptor and every tile live inside a stored (uncompressed) DZI zip, so a
/// tile is served with one range read and the zip is never extracted. Built at import time and
/// persisted next to the zip.
/// </summary>
public sealed class DziTileIndex
{
    private const uint Magic = 0x5A445049; // "IPDZ"
    private const byte FormatVersion = 2;

    private readonly TileEntry[] _tiles;

    public DziTileIndex(ZipRange descriptor, string tileExtension, IEnumerable<TileEntry> tiles, long zipLength)
    {
        Descriptor = descriptor;
        ZipLength = zipLength;
        TileExtension = tileExtension;
        _tiles = tiles.ToArray();
        Array.Sort(_tiles, TileEntry.Compare);
    }

    public ZipRange Descriptor { get; }

    /// <summary>Length of the zip the index was built from; a stored index is only valid for that zip.</summary>
    public long ZipLength { get; }

    /// <summary>Extension of every tile, without the dot (e.g. "webp").</summary>
    public string TileExtension { get; }

    public int TileCount => _tiles.Length;

    public bool TryGetTile(int level, int column, int row, out ZipRange range)
    {
        var key = new TileEntry(level, column, row, 0, 0);
        var lo = 0;
        var hi = _tiles.Length - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var cmp = TileEntry.Compare(_tiles[mid], key);
            if (cmp == 0)
            {
                range = new ZipRange(_tiles[mid].Offset, _tiles[mid].Length);
                return true;
            }
            if (cmp < 0) lo = mid + 1; else hi = mid - 1;
        }

        range = default;
        return false;
    }

    /// <summary>
    /// The tile of the highest level (at most <paramref name="maxLevel"/>) that consists of a
    /// single tile — the whole slide at small size, usable as a preview without decoding.
    /// </summary>
    public ZipRange? FindPreviewTile(int maxLevel = 12)
    {
        for (var level = maxLevel; level >= 0; level--)
        {
            var first = -1;
            var count = 0;
            for (var i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Level != level) continue;
                if (first < 0) first = i;
                count++;
            }
            if (count == 1)
                return new ZipRange(_tiles[first].Offset, _tiles[first].Length);
        }
        return null;
    }

    public void WriteTo(Stream output)
    {
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(TileExtension);
        writer.Write(ZipLength);
        writer.Write(Descriptor.Offset);
        writer.Write(Descriptor.Length);
        writer.Write(_tiles.Length);
        foreach (var tile in _tiles)
        {
            writer.Write(tile.Level);
            writer.Write(tile.Column);
            writer.Write(tile.Row);
            writer.Write(tile.Offset);
            writer.Write(tile.Length);
        }
    }

    public static DziTileIndex ReadFrom(Stream input)
    {
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic)
            throw new InvalidDataException("Not a DZI tile index.");
        var version = reader.ReadByte();
        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported DZI tile index version {version}.");

        var extension = reader.ReadString();
        var zipLength = reader.ReadInt64();
        var descriptor = new ZipRange(reader.ReadInt64(), reader.ReadInt64());
        var count = reader.ReadInt32();
        if (count < 0)
            throw new InvalidDataException("Corrupt DZI tile index.");

        var tiles = new TileEntry[count];
        for (var i = 0; i < count; i++)
            tiles[i] = new TileEntry(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt32());

        return new DziTileIndex(descriptor, extension, tiles, zipLength);
    }

    public readonly record struct TileEntry(int Level, int Column, int Row, long Offset, int Length)
    {
        public static int Compare(TileEntry a, TileEntry b)
        {
            var c = a.Level.CompareTo(b.Level);
            if (c != 0) return c;
            c = a.Column.CompareTo(b.Column);
            return c != 0 ? c : a.Row.CompareTo(b.Row);
        }
    }
}
