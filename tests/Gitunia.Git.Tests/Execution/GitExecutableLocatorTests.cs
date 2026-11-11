using Gitunia.Git.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public sealed class GitExecutableLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gitunia-locator-{Guid.NewGuid():N}");

    public GitExecutableLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void GivenFakeGitInWorkingDirectoryAndRealOneInPath_WhenLocating_ThenPathEntryWins()
    {
        var workingDirectory = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        var binDirectory = Directory.CreateDirectory(Path.Combine(_root, "bin")).FullName;
        var fake = CreateGit(workingDirectory, GitName());
        var real = CreateGit(binDirectory, GitName());
        var path = string.Join(Path.PathSeparator, ".", string.Empty, "work", binDirectory);
        var previous = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = workingDirectory;
            var located = new GitExecutableLocator(path, null, OperatingSystem.IsWindows()).Locate();

            Assert.Equal(real, located);
            Assert.NotEqual(fake, located);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }

    [Fact]
    public void GivenOnlyRelativeEntries_WhenLocating_ThenBareNameIsTheFallback()
    {
        var workingDirectory = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        CreateGit(workingDirectory, GitName());
        var previous = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = workingDirectory;
            var located = new GitExecutableLocator($".{Path.PathSeparator}", null, OperatingSystem.IsWindows()).Locate();

            Assert.Equal(GitExecutableLocator.BareName, located);
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }
    }

    [Fact]
    public void GivenNoPath_WhenLocating_ThenBareNameIsTheFallback()
    {
        Assert.Equal(GitExecutableLocator.BareName, new GitExecutableLocator(null, null, false).Locate());
    }

    [Fact]
    public void GivenWindowsRulesAndPathExt_WhenLocating_ThenExtensionsAreTriedInOrder()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        CreateGit(first, "git.txt");
        var expected = CreateGit(second, "git.cmd");
        var path = string.Join(Path.PathSeparator, first, second);

        var located = new GitExecutableLocator(path, ".exe;.cmd", true).Locate();

        Assert.Equal(expected, located);
    }

    [Fact]
    public void GivenGitForWindowsLauncher_WhenLocating_ThenRealBinaryIsPreferred()
    {
        var launcher = Directory.CreateDirectory(Path.Combine(_root, "Git", "cmd")).FullName;
        var realDirectory = Directory.CreateDirectory(Path.Combine(_root, "Git", "mingw64", "bin")).FullName;
        CreateGit(launcher, "git.exe");
        var real = CreateGit(realDirectory, "git.exe");

        var located = new GitExecutableLocator(launcher, ".exe", true).Locate();

        Assert.Equal(real, located);
    }

    [Fact]
    public void GivenLauncherWithoutRealBinary_WhenLocating_ThenLauncherIsKept()
    {
        var launcher = Directory.CreateDirectory(Path.Combine(_root, "Git", "cmd")).FullName;
        var expected = CreateGit(launcher, "git.exe");

        var located = new GitExecutableLocator(launcher, ".exe", true).Locate();

        Assert.Equal(expected, located);
    }

    [Fact]
    public void GivenRealGitOnPath_WhenLocatingFromEnvironment_ThenResultIsAbsolute()
    {
        var located = GitExecutableLocator.FromEnvironment().Locate();

        Assert.True(Path.IsPathRooted(located), located);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private static string GitName() => OperatingSystem.IsWindows() ? "git.exe" : "git";

    private static string CreateGit(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
