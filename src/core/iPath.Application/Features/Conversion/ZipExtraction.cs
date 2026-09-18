using System.IO.Compression;

namespace iPath.Application.Features.Conversion;

public static class ZipExtraction
{
    // Zip entries created on Windows can carry '\' separators (ZipFile.CreateEntryFromFile
    // writes the entry name verbatim). On Unix a backslash is a literal filename character,
    // so ZipFile.ExtractToDirectory flattens nested entries into the destination root.
    public static string NormalizeEntryPath(string entryFullName) => entryFullName.Replace('\\', '/');

    public static void ExtractNormalized(string sourceZipPath, string targetDirectory)
    {
        var root = Path.GetFullPath(targetDirectory);
        Directory.CreateDirectory(root);
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(sourceZipPath);
        foreach (var entry in archive.Entries)
        {
            var relative = NormalizeEntryPath(entry.FullName);
            if (string.IsNullOrEmpty(relative))
                continue;

            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(rootPrefix, StringComparison.Ordinal))
                throw new IOException($"Zip entry '{entry.FullName}' escapes the extraction directory.");

            if (entry.Name.Length == 0)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }
}
