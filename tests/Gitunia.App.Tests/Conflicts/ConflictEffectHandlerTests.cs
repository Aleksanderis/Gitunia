using Gitunia.App.Conflicts;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Conflicts;

// Real git on temporary repositories: the handler is the only place that reads, writes or re-creates the file.
public class ConflictEffectHandlerTests
{
    private const string ConflictPath = "conflict.txt";

    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenAConflictedFile_WhenReading_ThenThePartsComeBackWithTheOpenNumber()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadConflictEffect(Tab, location, ConflictPath, 4, ConflictKind.BothModified));

        var read = Assert.IsType<ConflictFileRead>(result);
        Assert.Equal(Tab, read.TabId);
        Assert.Equal(4, read.Generation);
        Assert.Equal(ConflictPath, read.Path);
        Assert.Equal(ConflictKind.BothModified, read.Conflict);
        Assert.Equal(ConflictReadKind.Text, read.Result.Kind);
        Assert.Equal(1, read.Result.HunkCount);
    }

    [Fact]
    public async Task GivenAFileThatIsNotConflicted_WhenReading_ThenTheReadersReasonComesBackNotAFailure()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadConflictEffect(Tab, location, "a.txt", 1, ConflictKind.BothModified));

        var read = Assert.IsType<ConflictFileRead>(result);
        Assert.NotEqual(ConflictReadKind.Text, read.Result.Kind);
        Assert.False(string.IsNullOrEmpty(read.Result.Reason));
    }

    [Fact]
    public async Task GivenAFailingReader_WhenReading_ThenTheFailureNamesTheFileAndTheReason()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);

        var result = await RunAsync(
            new ReadConflictEffect(Tab, location, ConflictPath, 1, ConflictKind.BothModified),
            new FailingReader(),
            null,
            null,
            TestContext.Current.CancellationToken);

        var failed = Assert.IsType<ConflictOperationFailed>(result);
        Assert.Equal("Could not read conflict.txt: boom", failed.Text);
        Assert.Equal(StatusSeverity.Error, failed.Severity);
    }

    [Fact]
    public async Task GivenAnUnchangedFile_WhenWriting_ThenTheTextIsWrittenAndSavedComesBack()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        var original = File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath));

        var result = await RunAsync(Write(location, original, "resolved\n"));

        var saved = Assert.IsType<ConflictSaved>(result);
        Assert.Equal("Saved conflict.txt", saved.Message);
        Assert.True(saved.IsComplete);
        Assert.Equal("resolved\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath)));
    }

    [Fact]
    public async Task GivenAFileEditedSinceItWasOpened_WhenWriting_ThenItIsNotOverwritten()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        var original = File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath));
        File.WriteAllText(Path.Combine(repository.WorkingTreePath, ConflictPath), "edited elsewhere\n");

        var result = await RunAsync(Write(location, original, "resolved\n"));

        var failed = Assert.IsType<ConflictOperationFailed>(result);
        Assert.Equal(ConflictNotices.FileChanged, failed.Text);
        Assert.Equal(StatusSeverity.Warning, failed.Severity);
        Assert.Equal("edited elsewhere\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath)));
    }

    [Fact]
    public async Task GivenAFailingWriter_WhenWriting_ThenTheFailureSaysThePicksAreKept()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        var original = File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath));

        var result = await RunAsync(
            Write(location, original, "resolved\n"),
            null,
            new FailingWriter(),
            null,
            TestContext.Current.CancellationToken);

        var failed = Assert.IsType<ConflictOperationFailed>(result);
        Assert.Equal("Could not save conflict.txt: disk full; your picks are kept", failed.Text);
        Assert.Equal(StatusSeverity.Error, failed.Severity);
    }

    [Fact]
    public async Task GivenAWreckedFile_WhenRecreating_ThenTheMarkersAreBackAndTheFileIsReadAgain()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        File.WriteAllText(Path.Combine(repository.WorkingTreePath, ConflictPath), "garbage\n");

        var result = await RunAsync(new RecreateConflictEffect(Tab, location, ConflictPath));

        var recreated = Assert.IsType<ConflictRecreated>(result);
        Assert.Equal(ConflictReadKind.Text, recreated.Result.Kind);
        Assert.Equal(1, recreated.Result.HunkCount);
        Assert.Contains(
            "<<<<<<<",
            File.ReadAllText(Path.Combine(repository.WorkingTreePath, ConflictPath)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenGitRefusesToRecreate_WhenRecreating_ThenTheFailureNamesTheFileAndTheReason()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);

        var result = await RunAsync(
            new RecreateConflictEffect(Tab, location, ConflictPath),
            null,
            null,
            new RefusingRecreator(),
            TestContext.Current.CancellationToken);

        var failed = Assert.IsType<ConflictOperationFailed>(result);
        Assert.Equal("Could not re-create the conflict of conflict.txt: refused", failed.Text);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenReading_ThenTheResultIsCancellationNotAFailure()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var result = await RunAsync(
            new ReadConflictEffect(Tab, location, ConflictPath, 1, ConflictKind.BothModified),
            new CancellingReader(),
            null,
            null,
            source.Token);

        Assert.Equal(Tab, Assert.IsType<ConflictOperationCancelled>(result).TabId);
    }

    private static WriteConflictEffect Write(RepositoryLocation location, string original, string text) =>
        new(Tab, location, ConflictPath, original, text, "Saved conflict.txt", true);

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(ConflictEffect effect) =>
        RunAsync(effect, null, null, null, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunAsync(
        ConflictEffect effect,
        IConflictReader? reader,
        IConflictWriter? writer,
        IConflictRecreator? recreator,
        CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new ConflictEffectHandler(
            reader ?? TestGit.Services.Conflicts,
            writer ?? TestGit.Services.ConflictWriter,
            recreator ?? TestGit.Services.ConflictRecreator,
            dispatcher);
        handler.Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FailingReader : IConflictReader
    {
        public Task<ConflictReadResult> ReadAsync(
            RepositoryLocation location,
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromException<ConflictReadResult>(new InvalidOperationException("boom"));
    }

    private sealed class CancellingReader : IConflictReader
    {
        public Task<ConflictReadResult> ReadAsync(
            RepositoryLocation location,
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(ConflictReadResult.Unsupported("unreachable"));
        }
    }

    private sealed class FailingWriter : IConflictWriter
    {
        public Task WriteAsync(
            RepositoryLocation location,
            string path,
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("disk full"));
    }

    private sealed class RefusingRecreator : IConflictRecreator
    {
        public Task RecreateAsync(
            RepositoryLocation location,
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new InvalidOperationException("refused"));
    }
}
