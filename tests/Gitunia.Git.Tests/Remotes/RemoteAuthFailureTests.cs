using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class RemoteAuthFailureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("user@host: Permission denied (publickey,password).\nfatal: Could not read from remote repository.")]
    [InlineData("fatal: could not read Username for 'https://example.com': terminal prompts disabled")]
    [InlineData("fatal: could not read Password for 'https://u@example.com': terminal prompts disabled")]
    [InlineData("remote: Invalid username or password.\nfatal: Authentication failed for 'https://example.com/r.git/'")]
    [InlineData("Host key verification failed.\nfatal: Could not read from remote repository.")]
    [InlineData("error: The requested URL returned error: 401")]
    public void GivenGitOrSshAuthenticationOutput_WhenMatching_ThenItIsAnAuthFailure(string standardError)
    {
        Assert.True(RemoteAuthFailure.Matches(standardError));
    }

    [Theory]
    [InlineData("fatal: '/no/such/repo' does not appear to be a git repository")]
    [InlineData("fatal: Could not read from remote repository.")]
    [InlineData("! [rejected]        main -> main (non-fast-forward)")]
    [InlineData("ssh: connect to host example.com port 22: Connection timed out")]
    [InlineData("fatal: unable to access 'https://example.com/r.git/': Could not resolve host: example.com")]
    public void GivenAnotherFailure_WhenMatching_ThenItIsNotAnAuthFailure(string standardError)
    {
        Assert.False(RemoteAuthFailure.Matches(standardError));
    }

    [Fact]
    public async Task GivenAnSshServerThatRefusesTheKey_WhenFetching_ThenGitsRealOutputIsAnAuthFailure()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake ssh is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        var directory = Path.Combine(Path.GetTempPath(), $"gitunia-auth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var script = Path.Combine(directory, "ssh");
            await File.WriteAllTextAsync(
                script,
                "#!/bin/sh\necho 'user@host: Permission denied (publickey).' >&2\nexit 255\n",
                Token);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            repository.Git("remote", "add", "remote-ssh", "ssh://example.invalid/repo.git");
            var writer = new RemoteWriter(
                new RemoteRunner(repository.CreateIsolatedRunner(), name => name == "GIT_SSH_COMMAND" ? script : null));
            var location = await new RepositoryDiscovery(repository.CreateIsolatedRunner())
                .DiscoverAsync(repository.WorkingTreePath, Token);

            var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
                () => writer.FetchAsync(location, new FetchRequest("remote-ssh", false, false), null, Token));

            Assert.True(RemoteAuthFailure.Matches(failure.StandardError));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task GivenARemoteThatDoesNotExist_WhenFetching_ThenItIsNotAnAuthFailure()
    {
        using var repository = TestRepositories.WithOrigin();
        var missing = Path.Combine(Path.GetTempPath(), $"gitunia-missing-{Guid.NewGuid():N}");
        repository.Git("remote", "add", "gone", missing);
        var writer = new RemoteWriter(new RemoteRunner(repository.CreateIsolatedRunner(), _ => null));
        var location = await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

        var failure = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => writer.FetchAsync(location, new FetchRequest("gone", false, false), null, Token));

        Assert.False(RemoteAuthFailure.Matches(failure.StandardError));
    }
}
