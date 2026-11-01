using Gitunia.App.Remotes;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Remotes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Remotes;

// Real git on temporary repositories: the handler is the only place that runs the remote commands.
public class RemoteEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenNoRemote_WhenAdding_ThenTheRemoteIsConfiguredAndNothingIsFetched()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new AddRemoteEffect(Tab, location, "backup", "https://example.com/b.git"));

        var success = Assert.IsType<RemoteOperationSucceeded>(result);
        Assert.Equal(
            "Added remote backup (https://example.com/b.git); run fetch to get its branches",
            success.Text);
        Assert.Equal("https://example.com/b.git", repository.Git("config", "remote.backup.url").Trim());
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenAdding_ThenNothingIsAddedAndTheFailureSaysSo()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new AddRemoteEffect(Tab, location, "bad name", "x"));

        var failure = Assert.IsType<RemoteOperationFailed>(result);
        Assert.StartsWith("Could not add the remote: ", failure.Text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("remote").Trim());
    }

    [Fact]
    public async Task GivenARemote_WhenListing_ThenItsNameAndUrlComeBackWithTheRequest()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);
        var target = new RemoteTarget(null, null);

        var result = await RunAsync(new ListRemotesEffect(Tab, location, RemoteAction.Rename, target));

        var listed = Assert.IsType<RemotesListed>(result);
        Assert.Equal(RemoteAction.Rename, listed.Action);
        Assert.Equal(target, listed.Target);
        var remote = Assert.Single(listed.Remotes);
        Assert.Equal("origin", remote.Name);
        Assert.Equal(repository.OriginPath, remote.Url);
    }

    [Fact]
    public async Task GivenARemoteWithBranches_WhenRemoving_ThenGitDropsItsTrackingBranchesAndTheTextHasTheRestoreHint()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RemoveRemoteEffect(Tab, location, "origin", repository.OriginPath));

        var text = Assert.IsType<RemoteOperationSucceeded>(result).Text;
        Assert.Equal(
            $"Removed remote origin (restore: git remote add origin {repository.OriginPath}, then fetch)",
            text);
        Assert.Equal(string.Empty, repository.Git("remote").Trim());
        Assert.Equal(string.Empty, repository.Git("branch", "-r").Trim());
    }

    [Fact]
    public async Task GivenAnUnknownRemote_WhenRemoving_ThenTheFailureIsReported()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RemoveRemoteEffect(Tab, location, "nope", "u"));

        Assert.StartsWith("Could not remove the remote: ", Assert.IsType<RemoteOperationFailed>(result).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemote_WhenRenaming_ThenGitMovesItsTrackingBranches()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RenameRemoteEffect(Tab, location, "origin", "upstream"));

        Assert.Equal("Renamed remote origin to upstream", Assert.IsType<RemoteOperationSucceeded>(result).Text);
        Assert.Equal("upstream", repository.Git("remote").Trim());
        Assert.Contains("upstream/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnExistingName_WhenRenaming_ThenTheFailureIsReported()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "other", "somewhere");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RenameRemoteEffect(Tab, location, "origin", "other"));

        Assert.StartsWith("Could not rename the remote: ", Assert.IsType<RemoteOperationFailed>(result).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARemote_WhenSettingTheUrl_ThenTheNewUrlIsStoredAndTheOldOneNamed()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        var effect = new SetRemoteUrlEffect(Tab, location, "origin", repository.OriginPath, "https://example.com/x.git");
        var result = await RunAsync(effect);

        Assert.Equal(
            $"Set the URL of origin to https://example.com/x.git (was {repository.OriginPath})",
            Assert.IsType<RemoteOperationSucceeded>(result).Text);
        Assert.Equal("https://example.com/x.git", repository.Git("config", "remote.origin.url").Trim());
    }

    [Fact]
    public async Task GivenAnUnknownRemote_WhenSettingTheUrl_ThenTheFailureIsReported()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new SetRemoteUrlEffect(Tab, location, "nope", "a", "b"));

        Assert.StartsWith("Could not set the URL: ", Assert.IsType<RemoteOperationFailed>(result).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenListing_ThenTheCancellationIsPostedInsteadOfAFailure()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var effect = new ListRemotesEffect(Tab, location, RemoteAction.Remove, new RemoteTarget(null, null));
        var result = await RunWithTokenAsync(effect, cancelled.Token);

        Assert.IsType<RemoteOperationCancelled>(result);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(RemoteEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(RemoteEffect effect, CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new RemoteEffectHandler(TestGit.Services.RemoteManager, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
