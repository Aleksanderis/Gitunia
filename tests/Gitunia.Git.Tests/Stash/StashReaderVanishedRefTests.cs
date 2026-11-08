using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Tests.Execution;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashReaderVanishedRefTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // "stash list" always fails; "rev-parse --verify refs/stash" exits 1 (ref gone) or 0 (ref exists).
    private static FakeGitExecutable StashListFailing(string message, bool stashRefExists)
    {
        var verifyExit = stashRefExists ? 0 : 1;
        return FakeGitExecutable.Create(
            $"case \"$*\" in *rev-parse*) exit {verifyExit};; esac\necho '{message}' >&2\nexit 128",
            $"echo %* | findstr /c:\"rev-parse\" >nul && exit /b {verifyExit}\r\necho {message} 1>&2\r\nexit /b 128");
    }

    private static StashReader ReaderFor(FakeGitExecutable fake) =>
        new(new GitRunner(new GitRunnerOptions(fake.ExecutablePath)));

    private static RepositoryLocation AnyLocation()
    {
        var path = Path.GetTempPath();
        return new RepositoryLocation(path, Path.Combine(path, ".git"), Path.Combine(path, ".git"), false, false);
    }

    [Fact]
    public async Task GivenStashRefVanishedDuringList_WhenReading_ThenListIsEmptyWithoutError()
    {
        using var fake = StashListFailing("fatal: bad revision refs/stash", stashRefExists: false);

        var result = await ReaderFor(fake).ReadStashesAsync(AnyLocation(), Token);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task GivenStashListFailsAndRefStillExists_WhenReading_ThenErrorSurfaces()
    {
        using var fake = StashListFailing("fatal: bad revision refs/stash", stashRefExists: true);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReaderFor(fake).ReadStashesAsync(AnyLocation(), Token));
    }

    [Fact]
    public async Task GivenOtherStashListFailure_WhenReading_ThenErrorSurfaces()
    {
        using var fake = FakeGitExecutable.Failing(128, "fatal: unable to read stash");

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReaderFor(fake).ReadStashesAsync(AnyLocation(), Token));
    }
}
