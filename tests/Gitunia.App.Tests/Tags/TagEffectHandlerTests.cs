using Gitunia.App.Tags;
using Gitunia.Core;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tags;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Tags;

// Real git on temporary repositories: the handler is the only place that runs the tag commands.
public class TagEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenACommit_WhenCreatingWithoutAMessage_ThenALightweightTagIsMadeThere()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new CreateTagEffect(Tab, location, "v1", repository.Head, string.Empty, "main"));

        var success = Assert.IsType<TagOperationSucceeded>(result);
        Assert.Equal("Created lightweight tag v1 at main", success.Text);
        Assert.Equal("commit", repository.Git("cat-file", "-t", "refs/tags/v1").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1").Trim());
    }

    [Fact]
    public async Task GivenAMessage_WhenCreating_ThenAnAnnotatedTagCarriesIt()
    {
        using var repository = TestRepositories.Linear();

        // The handler runs git with the real config, so an annotated tag needs an identity in the repository itself.
        repository.Git("config", "user.name", "Test");
        repository.Git("config", "user.email", "test@example.com");
        var location = await LocateAsync(repository);

        var effect = new CreateTagEffect(Tab, location, "v2", repository.Head, "Second release", "main");

        var result = await RunAsync(effect);

        var text = Assert.IsType<TagOperationSucceeded>(result).Text;
        Assert.Contains("annotated tag v2", text, StringComparison.Ordinal);
        Assert.Equal("tag", repository.Git("cat-file", "-t", "refs/tags/v2").Trim());
        Assert.Contains("Second release", repository.Git("cat-file", "-p", "refs/tags/v2"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenCreating_ThenNothingIsCreatedAndTheFailureNamesIt()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var effect = new CreateTagEffect(Tab, location, "bad name", repository.Head, string.Empty, "main");

        var result = await RunAsync(effect);

        var failure = Assert.IsType<TagOperationFailed>(result);
        Assert.Equal("Could not create the tag: 'bad name' is not a valid tag name", failure.Text);
        Assert.Null(failure.Retry);
        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenATag_WhenDeleting_ThenItIsGoneAndTheResultSaysHowToRestoreIt()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        var location = await LocateAsync(repository);
        var objectHash = repository.Git("rev-parse", "refs/tags/v1").Trim();

        var result = await RunAsync(new DeleteTagEffect(Tab, location, "v1", objectHash));

        var success = Assert.IsType<TagOperationSucceeded>(result);
        Assert.Equal($"Deleted tag v1 (was {objectHash[..7]})", success.Text);
        Assert.Equal($"Restore: git tag v1 {objectHash}", success.Detail);
        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenAMissingTag_WhenDeleting_ThenGitsErrorIsTheFailure()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new DeleteTagEffect(Tab, location, "nope", repository.Head));

        var failure = Assert.IsType<TagOperationFailed>(result);
        Assert.StartsWith("Could not delete the tag:", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTwoRemotes_WhenListing_ThenBothNamesComeBack()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("remote", "add", "backup", repository.OriginPath);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ListTagRemotesEffect(Tab, location, "v1", true));

        var loaded = Assert.IsType<TagRemotesLoaded>(result);
        Assert.Equal(["backup", "origin"], loaded.Remotes.Order(StringComparer.Ordinal));
        Assert.True(loaded.Push);
        Assert.Equal("v1", loaded.Name);
    }

    [Fact]
    public async Task GivenTwoTags_WhenPushingOne_ThenOnlyThatTagReachesTheRemote()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1").Tag("v2");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushTagEffect(Tab, location, "origin", "v1"));

        Assert.Equal("Pushed tag v1 to origin", Assert.IsType<TagOperationSucceeded>(result).Text);
        Assert.Equal("v1", repository.GitIn(repository.OriginPath, ["tag", "--list"]).Trim());
    }

    [Fact]
    public async Task GivenAPushedTag_WhenDeletingItOnTheRemote_ThenTheLocalTagStays()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("push", "-q", "origin", "refs/tags/v1");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new DeleteTagOnRemoteEffect(Tab, location, "origin", "v1"));

        var text = Assert.IsType<TagOperationSucceeded>(result).Text;
        Assert.Contains("Deleted tag v1 on origin", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.GitIn(repository.OriginPath, ["tag", "--list"]).Trim());
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenARejectedPush_WhenPushing_ThenGitsErrorIsTheFailureWithoutATerminalRetry()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("push", "-q", "origin", "refs/tags/v1");
        repository.Git("tag", "-f", "v1", "HEAD~1");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushTagEffect(Tab, location, "origin", "v1"));

        var failure = Assert.IsType<TagOperationFailed>(result);
        Assert.StartsWith("Could not push the tag:", failure.Text, StringComparison.Ordinal);
        Assert.Null(failure.Retry);
    }

    [Fact]
    public async Task GivenAPushThatNeedsCredentials_WhenItFails_ThenTheRetryCarriesTheSamePush()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        var writer = new AuthFailingTagWriter(TestGit.Services.Tags, "git@host: Permission denied (publickey).");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushTagEffect(Tab, location, "origin", "v1"), writer);

        var retry = Assert.IsType<TagOperationFailed>(result).Retry!;
        Assert.Equal("Tag push", retry.Noun);
        Assert.Equal(["push", "--", "origin", "refs/tags/v1"], retry.Arguments);
    }

    [Fact]
    public async Task GivenARemoteDeleteThatNeedsCredentials_WhenItFails_ThenTheRetryCarriesTheSameDelete()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        var writer = new AuthFailingTagWriter(TestGit.Services.Tags, "fatal: Authentication failed for 'x'");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new DeleteTagOnRemoteEffect(Tab, location, "origin", "v1"), writer);

        var retry = Assert.IsType<TagOperationFailed>(result).Retry!;
        Assert.Equal("Remote tag delete", retry.Noun);
        Assert.Equal(["push", "--delete", "--", "origin", "refs/tags/v1"], retry.Arguments);
    }

    [Fact]
    public async Task GivenACancelledTab_WhenCreating_ThenTheResultIsCancelledAndNothingIsCreated()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(
            new CreateTagEffect(Tab, location, "v1", repository.Head, string.Empty, "main"),
            null,
            cancelled.Token);

        Assert.IsType<TagOperationCancelled>(result);
        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(TagEffect effect, ITagWriter? writer = null) =>
        RunWithTokenAsync(effect, writer, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(
        TagEffect effect,
        ITagWriter? writer,
        CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new TagEffectHandler(writer ?? TestGit.Services.Tags, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
