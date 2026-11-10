using System.Net;
using System.Runtime.InteropServices;
using Gitunia.App.Updates;
using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class UpdateCommandTests : IDisposable
{
    private const string OldBinaryText = "old binary";

    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-update-test-").FullName;
    private readonly FakeReleaseServer _server = new();
    private readonly HttpClient _client;
    private readonly StringWriter _output = new();
    private readonly string _executable;

    public UpdateCommandTests()
    {
        _client = new HttpClient(_server);
        _executable = Path.Combine(_directory, "gitunia");
        File.WriteAllText(_executable, OldBinaryText);
    }

    public void Dispose()
    {
        _client.Dispose();
        _output.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task GivenTheLatestReleaseIsInstalled_WhenUpdating_ThenItReportsUpToDateAndDownloadsNothing()
    {
        FakeRelease.ServeLinux(_server, "1.2.3");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Success, exitCode);
        Assert.Contains("1.2.3 is up to date", _output.ToString());
        Assert.Equal(0, _server.CountRequestsEndingWith(".tar.gz"));
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenANewerRelease_WhenCheckingOnly_ThenTheExitCodeSaysSoAndNothingChanges()
    {
        FakeRelease.ServeLinux(_server, "1.3.0");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: true);

        Assert.Equal(UpdateExitCode.UpdateAvailable, exitCode);
        Assert.Contains("1.3.0 is available (you have 1.2.3)", _output.ToString());
        Assert.Equal(0, _server.CountRequestsEndingWith(".tar.gz"));
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenAnOlderRelease_WhenCheckingOnly_ThenItIsNotAnUpdate()
    {
        FakeRelease.ServeLinux(_server, "1.0.0");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: true);

        Assert.Equal(UpdateExitCode.Success, exitCode);
    }

    [Fact]
    public async Task GivenAScriptInstallAndANewerRelease_WhenUpdating_ThenTheBinaryIsReplacedByTheVerifiedOne()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake release binary is a shell script.");
        FakeRelease.ServeLinux(_server, "1.3.0");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Success, exitCode);
        Assert.Contains("Updated Gitunia 1.2.3 -> 1.3.0", _output.ToString());
        Assert.Contains("echo \"gitunia 1.3.0\"", File.ReadAllText(_executable));
        Assert.Equal(["gitunia"], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task GivenAWrongChecksum_WhenUpdating_ThenTheBinaryIsUntouchedAndTheManualStepsAreShown()
    {
        var archiveName = FakeRelease.ServeLinux(_server, "1.3.0");
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}.sha256", $"{new string('0', 64)}  {archiveName}\n");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("checksum mismatch", _output.ToString());
        Assert.Contains($"{FakeRelease.BaseUrl}{archiveName}", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenADownloadThatEndsEarly_WhenUpdating_ThenTheBinaryIsUntouched()
    {
        var archiveName = FakeRelease.ServeLinux(_server, "1.3.0");
        var archive = FakeRelease.TarGz("gitunia", FakeRelease.Script("1.3.0"));
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}", archive[..(archive.Length / 2)], archive.Length);

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("download failed", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenAnArchiveWithoutTheBinary_WhenUpdating_ThenTheBinaryIsUntouched()
    {
        var archiveName = "gitunia-1.3.0-linux-x64.tar.gz";
        var archive = FakeRelease.TarGz("gitunia-1.3.0-linux-x64/README.md", [1, 2, 3]);
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}", archive);
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}.sha256", $"{FakeRelease.Sha256(archive)}  {archiveName}");
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("1.3.0", [archiveName, $"{archiveName}.sha256"]));

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("holds no gitunia", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenANewBinaryThatReportsAnotherVersion_WhenUpdating_ThenTheBinaryIsUntouched()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake release binary is a shell script.");
        FakeRelease.ServeLinux(_server, "1.3.0", reportedVersion: "9.9.9");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("did not report version 1.3.0 (9.9.9)", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenANewBinaryThatCannotRun_WhenUpdating_ThenTheBinaryIsUntouched()
    {
        var archiveName = "gitunia-1.3.0-linux-x64.tar.gz";
        var archive = FakeRelease.TarGz("gitunia", [0, 1, 2, 3]);
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}", archive);
        _server.Serve($"{FakeRelease.BaseUrl}{archiveName}.sha256", $"{FakeRelease.Sha256(archive)}  {archiveName}");
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("1.3.0", [archiveName, $"{archiveName}.sha256"]));

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("it did not run", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenAReleaseWithoutAChecksumFile_WhenUpdating_ThenNothingIsInstalled()
    {
        var archiveName = FakeRelease.ServeLinux(_server, "1.3.0");
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("1.3.0", [archiveName]));

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("no checksum file", _output.ToString());
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenAReleaseWithoutAnArchiveForThisSystem_WhenUpdating_ThenItSaysSo()
    {
        FakeRelease.ServeLinux(_server, "1.3.0");

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false, Architecture.Arm64);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("has no archive for this system", _output.ToString());
    }

    [Theory]
    [InlineData(HostSystem.MacOs, "/opt/homebrew/bin/gitunia", "brew upgrade gitunia")]
    [InlineData(HostSystem.Windows, "C:\\Users\\me\\AppData\\Local\\Microsoft\\WinGet\\Packages\\g\\gitunia.exe", "winget upgrade Gitunia.Gitunia")]
    [InlineData(HostSystem.Linux, "/usr/bin/gitunia", "install.sh")]
    public async Task GivenAPackageManagedInstall_WhenUpdating_ThenTheCommandIsShownAndNothingIsDownloaded(
        HostSystem system,
        string path,
        string expectedCommandPart)
    {
        FakeRelease.ServeLinux(_server, "1.3.0");
        var facts = new InstallFacts(path, null, system, "ID=ubuntu\n");

        var exitCode = await RunAsync("1.2.3", facts, checkOnly: false);

        Assert.Equal(UpdateExitCode.Success, exitCode);
        Assert.Contains(expectedCommandPart, _output.ToString());
        Assert.Equal(0, _server.CountRequestsEndingWith(".tar.gz"));
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenAnUnrecognisedInstall_WhenUpdating_ThenTheArchiveAddressIsShownAndNothingIsReplaced()
    {
        var archiveName = FakeRelease.ServeLinux(_server, "1.3.0");
        var facts = new InstallFacts(_executable, null, HostSystem.Linux, null);

        var exitCode = await RunAsync("1.2.3", facts, checkOnly: false);

        Assert.Equal(UpdateExitCode.Success, exitCode);
        Assert.Contains($"{FakeRelease.BaseUrl}{archiveName}", _output.ToString());
        Assert.Equal(0, _server.CountRequestsEndingWith(".tar.gz"));
        Assert.Equal(OldBinaryText, File.ReadAllText(_executable));
    }

    [Fact]
    public async Task GivenADevelopmentBuild_WhenUpdating_ThenItRefusesWithoutAskingTheServer()
    {
        FakeRelease.ServeLinux(_server, "1.3.0");

        var exitCode = await RunAsync("0.0.0-dev", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("development build", _output.ToString());
        Assert.Empty(_server.Requests);
    }

    [Fact]
    public async Task GivenTheServerFails_WhenUpdating_ThenTheReasonAndTheReleasesPageAreShown()
    {
        _server.Fail(FakeRelease.LatestUrl, HttpStatusCode.Forbidden);

        var exitCode = await RunAsync("1.2.3", ScriptFacts(), checkOnly: false);

        Assert.Equal(UpdateExitCode.Failed, exitCode);
        Assert.Contains("could not read the latest release", _output.ToString());
        Assert.Contains(UpdateAdvice.ReleasesUrl, _output.ToString());
    }

    [Fact]
    public async Task GivenAnUpdateRun_WhenRequestsAreSent_ThenTheyCarryNoCredentials()
    {
        FakeRelease.ServeLinux(_server, "1.3.0");

        await RunAsync("1.2.3", ScriptFacts(), checkOnly: true);

        var request = Assert.Single(_server.Requests);
        Assert.Null(request.Headers.Authorization);
        Assert.DoesNotContain(request.Headers, header => header.Key is "Cookie" or "Authorization");
    }

    private InstallFacts ScriptFacts() => new(_executable, InstallMarker.ScriptValue, HostSystem.Linux, null);

    private async Task<int> RunAsync(
        string version,
        InstallFacts facts,
        bool checkOnly,
        Architecture architecture = Architecture.X64)
    {
        var releaseClient = new ReleaseClient(_client, new Uri(FakeRelease.LatestUrl));
        var command = new UpdateCommand(releaseClient, new ExecutableSwapper(renamesAside: false), _output);

        return await command.RunAsync(new UpdateRequest(version, facts, architecture, checkOnly), CancellationToken.None);
    }
}
