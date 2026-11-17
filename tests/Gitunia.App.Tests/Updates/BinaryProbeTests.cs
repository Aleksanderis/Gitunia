using System.Diagnostics;
using Gitunia.App.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class BinaryProbeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-probe-test-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task GivenABinaryJustUnpackedWhileOtherProcessesStart_WhenReadingTheVersion_ThenItReportsTheVersion()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake release binary is a shell script.");
        var archive = FakeRelease.TarGz("gitunia", FakeRelease.Script("1.3.0"));
        var archivePath = Path.Combine(_directory, "gitunia.tar.gz");
        await File.WriteAllBytesAsync(archivePath, archive, TestContext.Current.CancellationToken);
        using var stop = new CancellationTokenSource();
        var stopToken = stop.Token;
        var spawners = Enumerable.Range(0, 12).Select(_ => Task.Factory.StartNew(() => SpawnUntil(stopToken), TaskCreationOptions.LongRunning)).ToArray();

        try
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var binary = Path.Combine(_directory, $"gitunia-{attempt}");
                ReleaseArchive.ExtractBinary(archivePath, "gitunia", binary);

                var version = await BinaryProbe.ReadVersionAsync(binary, TestContext.Current.CancellationToken);

                Assert.Equal("1.3.0", version?.ToString());
            }
        }
        finally
        {
            await stop.CancelAsync();
            await Task.WhenAll(spawners);
        }
    }

    private static void SpawnUntil(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var process = Process.Start(new ProcessStartInfo("true") { UseShellExecute = false });
            process?.WaitForExit();
        }
    }
}
