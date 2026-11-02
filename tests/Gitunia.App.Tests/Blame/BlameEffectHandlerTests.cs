using System.Text;
using Gitunia.App.Blame;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Blame;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Blame;

// Real git on temporary repositories: the handler fills the store and posts the results in order.
public class BlameEffectHandlerTests
{
    private const int BigFileLines = 50_000;

    private static readonly TabId Tab = new(1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenACommittedFile_WhenBlaming_ThenTheLinesComeFirstThenTheFinishWithEveryLineOwned()
    {
        using var repository = TestRepository.Create()
            .Commit("first", new TestFile("a.txt", "one\ntwo\n"))
            .Commit("second", new TestFile("a.txt", "one\nTWO\nthree\n"));
        var fixture = new Fixture(await LocateAsync(repository));

        fixture.Handler.Start(fixture.Effect("a.txt", repository.Head, 1), Token);
        var loaded = Assert.IsType<BlameLoaded>(await fixture.NextAsync());
        var last = await fixture.UntilFinishedAsync();

        Assert.Equal(BlameContentKind.Text, loaded.Kind);
        Assert.Equal(3, loaded.LineCount);
        Assert.Equal(1, loaded.Generation);
        Assert.Equal(3, Assert.IsType<BlameFinished>(last).BlamedLines);
        var data = fixture.Store.Current!;
        Assert.Equal(["first", "second", "second"], Enumerable.Range(0, 3).Select(line => data.OwnerOf(line)!.Summary));
        Assert.Equal(["one", "TWO", "three"], data.Lines);
    }

    [Fact]
    public async Task GivenAFileOf50000Lines_WhenBlaming_ThenTheLinesAreInTheStoreWhenLoadedIsPostedBeforeTheBlameEnds()
    {
        using var repository = TestRepository.Create();
        var text = new StringBuilder();
        for (var line = 0; line < BigFileLines; line++)
        {
            text.Append("line ").Append(line).Append('\n');
        }

        repository.Commit("big", new TestFile("big.txt", text.ToString()));
        var fixture = new Fixture(await LocateAsync(repository));

        fixture.Handler.Start(fixture.Effect("big.txt", null, 4), Token);
        var loaded = Assert.IsType<BlameLoaded>(await fixture.NextAsync());

        Assert.Equal(BigFileLines, loaded.LineCount);
        Assert.Equal(BigFileLines, fixture.Store.Current!.Lines.Count);
        Assert.Equal("line 49999", fixture.Store.Current!.Lines[BigFileLines - 1]);
        var last = await fixture.UntilFinishedAsync();
        Assert.Equal(BigFileLines, Assert.IsType<BlameFinished>(last).BlamedLines);
        Assert.All(Enumerable.Range(0, BigFileLines), line => Assert.NotNull(fixture.Store.Current!.OwnerOf(line)));
    }

    [Fact]
    public async Task GivenAnEditedWorkingTreeFile_WhenBlaming_ThenTheNewLineIsUncommitted()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "one\ntwo\n"));
        repository.WriteFile("a.txt", "one\nnew\ntwo\n");
        var fixture = new Fixture(await LocateAsync(repository));

        fixture.Handler.Start(fixture.Effect("a.txt", null, 1), Token);
        await fixture.UntilFinishedAsync();

        var data = fixture.Store.Current!;
        Assert.Equal([false, true, false], Enumerable.Range(0, 3).Select(line => data.OwnerOf(line)!.IsUncommitted));
    }

    [Fact]
    public async Task GivenABinaryFile_WhenBlaming_ThenOnlyTheBinaryResultIsPostedAndTheStoreStaysEmpty()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "b.bin"), [1, 0, 2]);
        var fixture = new Fixture(await LocateAsync(repository));

        fixture.Handler.Start(fixture.Effect("b.bin", null, 1), Token);

        var loaded = Assert.IsType<BlameLoaded>(await fixture.NextAsync());
        Assert.Equal(BlameContentKind.Binary, loaded.Kind);
        Assert.Null(fixture.Store.Current);
    }

    [Fact]
    public async Task GivenAMissingPathAtARevision_WhenBlaming_ThenTheFailureNamesTheFileAndGit()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x"));
        var fixture = new Fixture(await LocateAsync(repository));

        fixture.Handler.Start(fixture.Effect("nope.txt", repository.Head, 3), Token);

        var failed = Assert.IsType<BlameFailed>(await fixture.NextAsync());
        Assert.Equal(3, failed.Generation);
        Assert.Contains("Could not blame nope.txt", failed.Text);
        Assert.Contains("fatal", failed.Text);
    }

    [Fact]
    public async Task GivenARunningBlame_WhenStopped_ThenTheStoreIsEmptyAndNoFinishIsPosted()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x\n"));
        var fixture = new Fixture(await LocateAsync(repository));
        fixture.Handler.Start(fixture.Effect("a.txt", null, 5), Token);
        await fixture.NextAsync();

        fixture.Handler.Stop(new StopBlameEffect(5));

        Assert.Null(fixture.Store.Current);
    }

    [Fact]
    public async Task GivenANewerBlame_WhenTheOlderOnesStopArrivesLate_ThenTheNewerDataStays()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x\n"));
        var fixture = new Fixture(await LocateAsync(repository));
        fixture.Handler.Start(fixture.Effect("a.txt", null, 7), Token);
        await fixture.UntilFinishedAsync();

        fixture.Handler.Stop(new StopBlameEffect(6));

        Assert.Equal(7, fixture.Store.Current!.Generation);
    }

    [Fact]
    public async Task GivenTheBlamedLines_WhenFindingText_ThenTheMatchIsPostedWithItsQuery()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "alpha\nbeta\nGamma\n"));
        var fixture = new Fixture(await LocateAsync(repository));
        fixture.Handler.Start(fixture.Effect("a.txt", null, 2), Token);
        await fixture.UntilFinishedAsync();

        fixture.Handler.Find(new FindBlameMatchEffect(2, "gAMMa", -1, true, 1));
        fixture.Handler.Find(new FindBlameMatchEffect(2, "absent", -1, true, 1));
        fixture.Handler.Find(new FindBlameMatchEffect(99, "alpha", -1, true, 1));

        var found = Assert.IsType<BlameMatchFound>(await fixture.NextAsync());
        Assert.Equal(("gAMMa", 2), (found.Query, found.Index));
        Assert.Equal(-1, Assert.IsType<BlameMatchFound>(await fixture.NextAsync()).Index);
        Assert.Equal(-1, Assert.IsType<BlameMatchFound>(await fixture.NextAsync()).Index);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    private sealed class Fixture
    {
        private readonly RecordingDispatcher _dispatcher = new();
        private readonly RepositoryLocation _location;

        public Fixture(RepositoryLocation location)
        {
            _location = location;
            Store = new BlameStore();
            Handler = new BlameEffectHandler(TestGit.Services.Blame, Store, _dispatcher);
        }

        public BlameStore Store { get; }

        public BlameEffectHandler Handler { get; }

        public StartBlameEffect Effect(string path, string? revision, long generation) =>
            new(Tab, _location, path, revision, generation);

        public Task<AppCommand> NextAsync() => _dispatcher.NextAsync(Token);

        // Progress commands may come in between; the finish is the last one.
        public async Task<AppCommand> UntilFinishedAsync()
        {
            AppCommand command;
            do
            {
                command = await NextAsync();
            }
            while (command is BlameProgressed or BlameLoaded);

            return command;
        }
    }
}
