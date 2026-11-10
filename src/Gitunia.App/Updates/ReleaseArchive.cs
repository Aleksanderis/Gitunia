using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Gitunia.App.Updates;

/// <summary>Checks and unpacks a downloaded release archive. Only the binary is read out, to a path the caller chose.</summary>
public static class ReleaseArchive
{
    private const string ZipExtension = ".zip";
    private const int ChecksumHexLength = 64;
    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    /// <summary>
    /// Checks <paramref name="archivePath"/> against the text of its <c>.sha256</c> file (<c>&lt;hash&gt;  &lt;name&gt;</c>);
    /// throws an <see cref="UpdateException"/> on a missing or different checksum.
    /// </summary>
    public static void VerifyChecksum(string archivePath, string checksumText)
    {
        var words = checksumText.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        var expected = words.Length > 0 ? words[0] : string.Empty;
        var fileName = Path.GetFileName(archivePath);
        if (expected.Length != ChecksumHexLength)
        {
            throw new UpdateException($"no checksum for {fileName}");
        }

        using var stream = File.OpenRead(archivePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException($"checksum mismatch for {fileName} (expected {expected}, got {actual})");
        }
    }

    /// <summary>
    /// Writes the file named <paramref name="binaryName"/> from the archive (a <c>.zip</c> or a <c>.tar.gz</c>, found at any
    /// depth) to <paramref name="destination"/>; throws an <see cref="UpdateException"/> when the archive has none.
    /// </summary>
    public static void ExtractBinary(string archivePath, string binaryName, string destination)
    {
        try
        {
            var isZip = archivePath.EndsWith(ZipExtension, StringComparison.OrdinalIgnoreCase);
            var found = isZip
                ? ExtractFromZip(archivePath, binaryName, destination)
                : ExtractFromTar(archivePath, binaryName, destination);
            if (!found)
            {
                throw new UpdateException($"the archive holds no {binaryName}");
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            throw new UpdateException($"could not unpack {Path.GetFileName(archivePath)} ({exception.Message})", exception);
        }
    }

    private static bool ExtractFromZip(string archivePath, string binaryName, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (entry.Name == binaryName)
            {
                entry.ExtractToFile(destination, overwrite: true);

                return true;
            }
        }

        return false;
    }

    private static bool ExtractFromTar(string archivePath, string binaryName, string destination)
    {
        using var file = File.OpenRead(archivePath);
        using var unzipped = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new TarReader(unzipped);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType == TarEntryType.RegularFile && Path.GetFileName(entry.Name) == binaryName)
            {
                entry.ExtractToFile(destination, overwrite: true);

                return true;
            }
        }

        return false;
    }
}
