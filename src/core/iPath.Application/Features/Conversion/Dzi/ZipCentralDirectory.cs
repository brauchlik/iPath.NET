using System.Buffers.Binary;
using System.Text;

namespace iPath.Application.Features.Conversion.Dzi;

public readonly record struct ZipRange(long Offset, long Length);

public sealed record ZipCentralEntry(
    string Name,
    ushort Flags,
    ushort CompressionMethod,
    long CompressedSize,
    long UncompressedSize,
    long LocalHeaderOffset)
{
    public bool IsEncrypted => (Flags & 0x0001) != 0;
    public bool IsStored => CompressionMethod == 0;
    public bool IsDirectory => Name.EndsWith('/');
}

/// <summary>
/// Minimal reader for a zip's central directory, including ZIP64. Exists because
/// <see cref="System.IO.Compression.ZipArchive"/> does not expose entry offsets, which range
/// reads into a stored zip need.
/// </summary>
public static class ZipCentralDirectory
{
    private const uint EndOfCentralDirectorySignature = 0x06054b50;
    private const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064b50;
    private const uint Zip64EndOfCentralDirectorySignature = 0x06064b50;
    private const uint CentralDirectoryHeaderSignature = 0x02014b50;
    private const uint LocalFileHeaderSignature = 0x04034b50;
    private const int EndOfCentralDirectorySize = 22;
    private const int Zip64LocatorSize = 20;
    private const int CentralDirectoryHeaderSize = 46;
    private const int LocalFileHeaderSize = 30;
    private const ushort Zip64ExtraFieldId = 0x0001;

    public static IReadOnlyList<ZipCentralEntry> ReadEntries(Stream zip)
    {
        if (!zip.CanSeek || !zip.CanRead)
            throw new ArgumentException("The zip stream must be readable and seekable.", nameof(zip));

        var (entryCount, directorySize, directoryOffset) = ReadEndOfCentralDirectory(zip);

        if (directoryOffset < 0 || directorySize < 0 || directoryOffset + directorySize > zip.Length)
            throw new InvalidDataException("The zip central directory lies outside the file.");
        if (directorySize > int.MaxValue)
            throw new InvalidDataException("The zip central directory is too large.");

        var directory = new byte[directorySize];
        zip.Position = directoryOffset;
        zip.ReadExactly(directory);

        var entries = new List<ZipCentralEntry>((int)Math.Min(entryCount, 1_000_000));
        var pos = 0;
        for (long i = 0; i < entryCount; i++)
        {
            var header = directory.AsSpan(pos);
            if (header.Length < CentralDirectoryHeaderSize
                || BinaryPrimitives.ReadUInt32LittleEndian(header) != CentralDirectoryHeaderSignature)
                throw new InvalidDataException($"Corrupt zip central directory at entry {i}.");

            var flags = BinaryPrimitives.ReadUInt16LittleEndian(header[8..]);
            var method = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
            long compressed = BinaryPrimitives.ReadUInt32LittleEndian(header[20..]);
            long uncompressed = BinaryPrimitives.ReadUInt32LittleEndian(header[24..]);
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
            var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[30..]);
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(header[32..]);
            long localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[42..]);

            var variableLength = nameLength + extraLength + commentLength;
            if (header.Length < CentralDirectoryHeaderSize + variableLength)
                throw new InvalidDataException($"Corrupt zip central directory at entry {i}.");

            var nameBytes = header.Slice(CentralDirectoryHeaderSize, nameLength);
            var name = (flags & 0x0800) != 0 ? Encoding.UTF8.GetString(nameBytes) : Encoding.Latin1.GetString(nameBytes);

            var extra = header.Slice(CentralDirectoryHeaderSize + nameLength, extraLength);
            ApplyZip64Extra(extra, ref uncompressed, ref compressed, ref localHeaderOffset);

            entries.Add(new ZipCentralEntry(name, flags, method, compressed, uncompressed, localHeaderOffset));
            pos += CentralDirectoryHeaderSize + variableLength;
        }

        return entries;
    }

    /// <summary>Resolves where the entry's data starts, from its local header.</summary>
    public static ZipRange ResolveDataRange(Stream zip, ZipCentralEntry entry)
    {
        // The local header's extra field may differ from the central directory's, so its
        // length must be read from the local header itself.
        Span<byte> header = stackalloc byte[LocalFileHeaderSize];
        zip.Position = entry.LocalHeaderOffset;
        zip.ReadExactly(header);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalFileHeaderSignature)
            throw new InvalidDataException($"Corrupt local header for zip entry '{entry.Name}'.");

        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(header[26..]);
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(header[28..]);
        var dataOffset = entry.LocalHeaderOffset + LocalFileHeaderSize + nameLength + extraLength;

        if (dataOffset + entry.CompressedSize > zip.Length)
            throw new InvalidDataException($"Zip entry '{entry.Name}' extends past the end of the file.");

        return new ZipRange(dataOffset, entry.CompressedSize);
    }

    private static (long EntryCount, long DirectorySize, long DirectoryOffset) ReadEndOfCentralDirectory(Stream zip)
    {
        var tailLength = (int)Math.Min(zip.Length, EndOfCentralDirectorySize + ushort.MaxValue);
        if (tailLength < EndOfCentralDirectorySize)
            throw new InvalidDataException("The file is too small to be a zip.");

        var tail = new byte[tailLength];
        var tailStart = zip.Length - tailLength;
        zip.Position = tailStart;
        zip.ReadExactly(tail);

        var eocd = -1;
        for (var i = tailLength - EndOfCentralDirectorySize; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i)) == EndOfCentralDirectorySignature)
            {
                eocd = i;
                break;
            }
        }
        if (eocd < 0)
            throw new InvalidDataException("No zip end-of-central-directory record found.");

        var record = tail.AsSpan(eocd);
        long entryCount = BinaryPrimitives.ReadUInt16LittleEndian(record[10..]);
        long directorySize = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
        long directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(record[16..]);

        var needsZip64 = entryCount == ushort.MaxValue || directorySize == uint.MaxValue || directoryOffset == uint.MaxValue;
        var locatorPosition = tailStart + eocd - Zip64LocatorSize;
        if (locatorPosition >= 0)
        {
            Span<byte> locator = stackalloc byte[Zip64LocatorSize];
            zip.Position = locatorPosition;
            zip.ReadExactly(locator);
            if (BinaryPrimitives.ReadUInt32LittleEndian(locator) == Zip64EndOfCentralDirectoryLocatorSignature)
            {
                var zip64RecordOffset = BinaryPrimitives.ReadInt64LittleEndian(locator[8..]);
                Span<byte> zip64 = stackalloc byte[56];
                zip.Position = zip64RecordOffset;
                zip.ReadExactly(zip64);
                if (BinaryPrimitives.ReadUInt32LittleEndian(zip64) != Zip64EndOfCentralDirectorySignature)
                    throw new InvalidDataException("Corrupt ZIP64 end-of-central-directory record.");

                return (BinaryPrimitives.ReadInt64LittleEndian(zip64[32..]),
                        BinaryPrimitives.ReadInt64LittleEndian(zip64[40..]),
                        BinaryPrimitives.ReadInt64LittleEndian(zip64[48..]));
            }
        }

        if (needsZip64)
            throw new InvalidDataException("The zip requires ZIP64 but has no ZIP64 end-of-central-directory record.");

        return (entryCount, directorySize, directoryOffset);
    }

    private static void ApplyZip64Extra(ReadOnlySpan<byte> extra, ref long uncompressed, ref long compressed, ref long localHeaderOffset)
    {
        while (extra.Length >= 4)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(extra);
            var size = BinaryPrimitives.ReadUInt16LittleEndian(extra[2..]);
            if (extra.Length < 4 + size)
                return;

            if (id == Zip64ExtraFieldId)
            {
                // Only the fields whose 32-bit value is saturated are present, in this order.
                var data = extra.Slice(4, size);
                if (uncompressed == uint.MaxValue && data.Length >= 8) { uncompressed = BinaryPrimitives.ReadInt64LittleEndian(data); data = data[8..]; }
                if (compressed == uint.MaxValue && data.Length >= 8) { compressed = BinaryPrimitives.ReadInt64LittleEndian(data); data = data[8..]; }
                if (localHeaderOffset == uint.MaxValue && data.Length >= 8) { localHeaderOffset = BinaryPrimitives.ReadInt64LittleEndian(data); }
                return;
            }

            extra = extra[(4 + size)..];
        }
    }
}
