using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Platform;
using Gitunia.Tui.App;
using Terminal.Gui.Drivers;
using Xunit;

namespace Gitunia.Tui.Tests.App.Startup;

/// <summary>
/// Builds the whole shell the way <c>Program.Main</c> does (every controller, presenter and event wire-up in
/// <see cref="TuiApplication.Run"/>) on a headless driver and draws once. Constructor-order crashes (a null field used
/// by a callback, a key controller used before it exists) throw here instead of on the user's first start.
/// </summary>
public class StartupSmokeTests
{
    [Fact]
    public async Task GivenARepository_WhenStartingTheShell_ThenItBuildsAndRendersAFrameWithoutThrowing()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "one\n"));
        using var directories = new TempDirectories();
        var location = await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

        var exitCode = Start(directories, location);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void GivenNoRepositoryAndNoSession_WhenStartingTheShell_ThenThePickerShowsWithoutThrowing()
    {
        using var directories = new TempDirectories();

        var exitCode = Start(directories, null);

        Assert.Equal(0, exitCode);
    }

    private static int Start(TempDirectories directories, RepositoryLocation? location)
    {
        var capabilities = new ConsoleCapabilities(true, ColorDepth.TrueColor, true);
        var platform = new TestPlatform(directories, capabilities);
        using var application = new TuiApplication(TestGit.Services, capabilities, platform)
        {
            DriverName = DriverRegistry.Names.ANSI,
            ExitAfterFirstIteration = true,
        };

        return application.Run(location);
    }

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
    }
}
