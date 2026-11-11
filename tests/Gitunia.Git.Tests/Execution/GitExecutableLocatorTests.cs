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
    public void GivenFakeGitInRelativeEntryAndRealOneInPath_WhenLocating_ThenPathEntryWins()
    {
        var fakeDirectory = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        var binDirectory = Directory.CreateDirectory(Path.Combine(_root, "bin")).FullName;
        var fake = CreateGit(fakeDirectory, GitName());
        var real = CreateGit(binDirectory, GitName());
        var relativeToCurrent = Path.GetRelativePath(Environment.CurrentDirectory, fakeDirectory);
        var path = string.Join(Path.PathSeparator, ".", string.Empty, relativeToCurrent, binDirectory);

        var located = new GitExecutableLocator(path, null, OperatingSystem.IsWindows()).Locate();

        // The extension keeps the case of PATHEXT on Windows (git.EXE), the file system does not care.
        Assert.Equal(real, located, ignoreCase: OperatingSystem.IsWindows());
        Assert.NotEqual(fake, located, ignoreCase: OperatingSystem.IsWindows());
    }

    [Fact]
    public void GivenOnlyRelativeEntries_WhenLocating_ThenBareNameIsTheFallback()
    {
        var fakeDirectory = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        CreateGit(fakeDirectory, GitName());
        var relativeToCurrent = Path.GetRelativePath(Environment.CurrentDirectory, fakeDirectory);
        var path = string.Join(Path.PathSeparator, ".", relativeToCurrent);

        var located = new GitExecutableLocator(path, null, OperatingSystem.IsWindows()).Locate();

        Assert.Equal(GitExecutableLocator.BareName, located);
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

        Assert.True(Path.IsPathFullyQualified(located), located);
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
