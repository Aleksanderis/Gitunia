using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class GitVersionDetectorTests
{
    [Fact]
    public async Task GivenSupportedGit_WhenDetecting_ThenVersionIsReturned()
    {
        using var fake = FakeGitExecutable.Printing("git version 2.50.1");
        var detector = new GitVersionDetector(new GitRunner(new GitRunnerOptions(fake.ExecutablePath)));

        var version = await detector.DetectAsync(TestContext.Current.CancellationToken);

        Assert.Equal("2.50.1", version.ToString());
    }

    [Fact]
    public async Task GivenOldGit_WhenDetecting_ThenVersionTooOldNamesBothVersions()
    {
        using var fake = FakeGitExecutable.Printing("git version 2.30.0");
        var detector = new GitVersionDetector(new GitRunner(new GitRunnerOptions(fake.ExecutablePath)));

        var exception = await Assert.ThrowsAsync<GitVersionTooOldException>(
            () => detector.DetectAsync(TestContext.Current.CancellationToken));

        Assert.Contains("2.30.0", exception.Message);
        Assert.Contains("2.43.0", exception.Message);
    }

    [Fact]
    public async Task GivenMissingGit_WhenDetecting_ThenGitNotFoundIsThrown()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"gitunia-missing-{Guid.NewGuid():N}", "git");
        var detector = new GitVersionDetector(new GitRunner(new GitRunnerOptions(missing)));

        await Assert.ThrowsAsync<GitNotFoundException>(
            () => detector.DetectAsync(TestContext.Current.CancellationToken));
    }
}
