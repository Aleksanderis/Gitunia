using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Platform;
using Gitunia.Tui.App;
using Terminal.Gui.Drivers;
using Xunit;

namespace Gitunia.Tui.Tests.App.Startup;

/// <summary>
/// Builds the whole shell the way <c>Program.Main</c> does (every controller, presenter and event wire-up in
/// <see cref="TuiApplication.Run"/>) on a headless driver and draws its first frame once. Constructor-order crashes
/// (a null field used by a callback, a key controller used before it exists) throw here instead of on the first start.
/// </summary>
public class StartupSmokeTests
{
    [Fact]
    public async Task GivenARepository_WhenStartingTheShell_ThenItDrawsTheFirstFrameWithStatusBarAndHistory()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));
        using var directories = new TempDirectories();
        var location = await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

        var result = Start(directories, location);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NORMAL", result.Frame);
        Assert.Contains("History", result.Frame);
    }

    [Fact]
    public void GivenNoRepositoryAndNoSession_WhenStartingTheShell_ThenTheFirstFrameShowsThePicker()
    {
        using var directories = new TempDirectories();

        var result = Start(directories, null);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("no repository open", result.Frame);
    }

    private static StartResult Start(TempDirectories directories, RepositoryLocation? location)
    {
        var capabilities = new ConsoleCapabilities(IsInteractive: true, Color: ColorDepth.TrueColor, Unicode: true);
        var platform = new TestPlatform(directories, capabilities);
        var frame = string.Empty;
        using var application = new TuiApplication(TestGit.Services, capabilities, platform)
        {
            HomeDirectory = directories.Root,
            DriverName = DriverRegistry.Names.ANSI,
            ExitAfterFirstIteration = true,
            FrameCaptured = text => frame = text,
        };

        var exitCode = application.Run(location);

        return new StartResult(exitCode, frame);
    }

    private sealed record StartResult(int ExitCode, string Frame);

    private sealed class TempDirectories : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("gitunia-smoke-").FullName;

        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class TestPlatform(TempDirectories directories, ConsoleCapabilities capabilities) : IPlatform
    {
        public string ConfigDirectory => Path.Combine(directories.Root, "config");

        public string StateDirectory => Path.Combine(directories.Root, "state");

        public ConsoleCapabilities DetectConsole() => capabilities;

        public bool? QueryLightBackground(ConsoleCapabilities detected) => null;
    }
}
