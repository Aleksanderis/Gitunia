using Xunit;

namespace Gitunia.Git.Tests;

public class GitVersionTests
{
    [Theory]
    [InlineData("git version 2.43.0", 2, 43, 0)]
    [InlineData("git version 2.51.0.windows.1\n", 2, 51, 0)]
    [InlineData("git version 2.39.5 (Apple Git-154)", 2, 39, 5)]
    [InlineData("git version 2.48.rc1", 2, 48, 0)]
    public void GivenVersionOutput_WhenParsing_ThenNumbersAreExtracted(string text, int major, int minor, int patch)
    {
        Assert.True(GitVersion.TryParse(text, out var version));
        Assert.Equal(new GitVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("something else")]
    [InlineData("git version x.y")]
    public void GivenGarbage_WhenParsing_ThenFalse(string text)
    {
        Assert.False(GitVersion.TryParse(text, out _));
    }

    [Fact]
    public void GivenVersions_WhenComparing_ThenMinimumIsInclusive()
    {
        Assert.True(new GitVersion(2, 43, 0).IsAtLeast(GitVersion.Minimum));
        Assert.True(new GitVersion(3, 0, 0).IsAtLeast(GitVersion.Minimum));
        Assert.False(new GitVersion(2, 42, 9).IsAtLeast(GitVersion.Minimum));
    }

    [Fact]
    public async Task GivenOldGit_WhenDetecting_ThenTooOldError()
    {
        using var fake = FakeGitExecutable.Printing("git version 2.30.1");

        var error = await Assert.ThrowsAsync<GitVersionTooOldException>(
            () => GitVersionDetector.DetectAsync(new GitRunner(new GitRunnerOptions(fake.Path)), TestContext.Current.CancellationToken));

        Assert.Equal(new GitVersion(2, 30, 1), error.Found);
    }

    [Fact]
    public async Task GivenNewGit_WhenDetecting_ThenVersionReturned()
    {
        using var fake = FakeGitExecutable.Printing("git version 2.50.0");

        var version = await GitVersionDetector.DetectAsync(new GitRunner(new GitRunnerOptions(fake.Path)), TestContext.Current.CancellationToken);

        Assert.Equal(new GitVersion(2, 50, 0), version);
        Assert.True(version.IsAtLeast(GitVersion.RelativeWorktrees));
    }

    [Fact]
    public async Task GivenUnparsableOutput_WhenDetecting_ThenGitError()
    {
        using var fake = FakeGitExecutable.Printing("hello");

        await Assert.ThrowsAsync<GitException>(
            () => GitVersionDetector.DetectAsync(new GitRunner(new GitRunnerOptions(fake.Path)), TestContext.Current.CancellationToken));
    }
}
