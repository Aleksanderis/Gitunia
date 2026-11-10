using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Gitunia.App.Tests.Updates;

/// <summary>Builds the files of a fake release (archive and checksum) and registers them on a <see cref="FakeReleaseServer"/>.</summary>
public static class FakeRelease
{
    public const string LatestUrl = "https://releases.test/latest";
    public const string BaseUrl = "https://releases.test/download/";

    /// <summary>Serves the latest-release JSON and a linux-x64 archive of a binary that answers <c>--version</c>.</summary>
    public static string ServeLinux(FakeReleaseServer server, string version, string? reportedVersion = null)
    {
        var archiveName = $"gitunia-{version}-linux-x64.tar.gz";
        var archive = TarGz($"gitunia-{version}-linux-x64/gitunia", Script(reportedVersion ?? version));
        server.Serve($"{BaseUrl}{archiveName}", archive);
        server.Serve($"{BaseUrl}{archiveName}.sha256", $"{Sha256(archive)}  {archiveName}\n");
        server.Serve(LatestUrl, Json(version, [archiveName, $"{archiveName}.sha256"]));

        return archiveName;
    }

    public static string Json(string version, IEnumerable<string> assetNames)
    {
        var assets = string.Join(
            ",",
            assetNames.Select(name => $"{{\"name\":\"{name}\",\"browser_download_url\":\"{BaseUrl}{name}\"}}"));

        return $"{{\"tag_name\":\"v{version}\",\"assets\":[{assets}]}}";
    }

    public static string Sha256(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static byte[] Script(string version) => Encoding.UTF8.GetBytes($"#!/bin/sh\necho \"gitunia {version}\"\n");

    public static byte[] TarGz(string entryName, byte[] content)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(zip, TarEntryFormat.Pax))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName)
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                DataStream = new MemoryStream(content),
            };
            writer.WriteEntry(entry);
        }

        return output.ToArray();
    }

    public static byte[] Zip(string entryName, byte[] content)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            stream.Write(content);
        }

        return output.ToArray();
    }
}
