using Gitunia.App.DiffPane;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.DiffPane;
using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.DiffPane;

public class DiffPaneEffectHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenACommit_WhenLoadingItsDiff_ThenTheStoreLoadsTheDiffOfThatCommit()
    {
        using var rig = await Rig.CreateAsync();

        rig.Handler.Handle(new LoadDiffEffect(rig.TabId, rig.Target(0), true));
        await rig.Pump.RunUntilAsync(() => rig.Store.Status == DiffStatus.Loaded);

        Assert.Equal(rig.Session.Graph[0].Id, rig.Store.Commit);
        Assert.Equal([["a.txt"], ["b.txt"]], rig.Store.Document.FilePaths);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenSettingOtherOptions_ThenTheCommitLoadsAgainWithThem()
    {
        using var rig = await Rig.CreateAsync();
        rig.Handler.Handle(new LoadDiffEffect(rig.TabId, rig.Target(0), false));
        await rig.Pump.RunUntilAsync(() => rig.Store.Status == DiffStatus.Loaded);
        var options = rig.Store.Options with { WholeFile = true };

        rig.Handler.Handle(new SetDiffOptionsEffect(rig.TabId, options));
        await rig.Pump.RunUntilAsync(() => rig.Store.Status == DiffStatus.Loaded);

        Assert.Equal(options, rig.Store.Options);
        Assert.Equal(rig.Session.Graph[0].Id, rig.Store.Commit);
    }

    [Fact]
    public async Task GivenALoadedDiff_WhenClearing_ThenTheStoreAndTheContentStoreAreEmpty()
    {
        using var rig = await Rig.CreateAsync();
        rig.Handler.Handle(new LoadDiffEffect(rig.TabId, rig.Target(0), false));
        await rig.Pump.RunUntilAsync(() => rig.Store.Status == DiffStatus.Loaded);
        rig.Handler.Handle(new LoadFileContentEffect(rig.TabId, rig.FileRow("a.txt"), rig.Session.Graph[0].Id, TimeSpan.Zero));

        rig.Handler.Handle(new ClearDiffEffect(rig.TabId));

        Assert.Equal(DiffStatus.None, rig.Store.Status);
        Assert.Null(rig.Store.Commit);
        Assert.Same(FileContentState.Empty, rig.ContentStore.Current);
    }

    [Fact]
    public async Task GivenAFileRow_WhenLoadingItsContent_ThenTheStoreReadsTheBlob()
    {
        using var rig = await Rig.CreateAsync();
        var commit = rig.Session.Graph[0].Id;

        rig.Handler.Handle(new LoadFileContentEffect(rig.TabId, rig.FileRow("a.txt"), commit, TimeSpan.Zero));
        await rig.Pump.RunUntilAsync(() => rig.ContentStore.Current.Status == CommitDetailsStatus.Loaded);

        Assert.Equal("a.txt", rig.ContentStore.Current.Path);
        Assert.Equal(commit, rig.ContentStore.Current.Commit);
    }

    [Fact]
    public async Task GivenOpenContent_WhenClearingIt_ThenTheContentStoreForgetsTheFile()
    {
        using var rig = await Rig.CreateAsync();
        rig.Handler.Handle(
            new LoadFileContentEffect(rig.TabId, rig.FileRow("a.txt"), rig.Session.Graph[0].Id, TimeSpan.Zero));

        rig.Handler.Handle(new ClearFileContentEffect(rig.TabId));

        Assert.Same(FileContentState.Empty, rig.ContentStore.Current);
    }

    [Fact]
    public async Task GivenATabWithoutASession_WhenAnyEffectArrives_ThenNothingHappens()
    {
        using var rig = await Rig.CreateAsync();
        var gone = new TabId(999);

        rig.Handler.Handle(new LoadDiffEffect(gone, rig.Target(0), true));
        rig.Handler.Handle(new ClearDiffEffect(gone));

        Assert.Equal(DiffStatus.None, rig.Store.Status);
    }

    private sealed class Rig : ITabSessions, IDisposable
    {
        private readonly TestRepository _repository;

        private Rig(TestRepository repository, TabSession session, UiPump pump, DiffStore store, FileContentStore content)
        {
            _repository = repository;
            Session = session;
            Pump = pump;
            Store = store;
            ContentStore = content;
            TabId = session.TabId;
            Handler = new DiffPaneEffectHandler(this);
        }

        public TabSession Session { get; }

        public UiPump Pump { get; }

        public DiffStore Store { get; }

        public FileContentStore ContentStore { get; }

        public TabId TabId { get; }

        public DiffPaneEffectHandler Handler { get; }

        public static async Task<Rig> CreateAsync()
        {
            var repository = TestRepository.Create()
                .Commit("root", new TestFile("a.txt", "one\ntwo\n"), new TestFile("b.txt", "b\n"))
                .Commit("edit", new TestFile("a.txt", "one\nchanged\n"), new TestFile("b.txt", "b changed\n"));
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
            var host = new Dispatcher(AppState.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var pump = new UiPump(Token);
            var store = new DiffStore(TestGit.Services.Diff, location, pump);
            var content = new FileContentStore(TestGit.Services.Blob, location, pump);
            var session = new TabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(TestGit.Services.CommitDetails, location, pump, TimeSpan.Zero),
                    store,
                    ContentStore: content));
            await TestGit.Services.RepositoryLoader.RunAsync(
                location,
                session,
                host,
                new StatusReporter(new Dispatcher(AppState.Empty)),
                InlineUi.Instance,
                session.Cancellation.Token);

            return new Rig(repository, session, pump, store, content);
        }

        public DiffTarget Target(int graphIndex)
        {
            // The history is linear, so the parent of a commit is the next row.
            var parent = Session.Graph[graphIndex + 1].Id;

            return new DiffTarget(Session.Graph[graphIndex].Id, parent, false, false);
        }

        public FileTreeRow FileRow(string path)
        {
            return new FileTreeRow(FileTreeRowKind.File, path, path, 0, false, 8, 0, BlobOf(path));
        }

        public TabSession? Find(TabId tabId) => tabId == TabId ? Session : null;

        public void Dispose()
        {
            Session.Dispose();
            _repository.Dispose();
        }

        private ObjectId BlobOf(string path)
        {
            var output = _repository.Git("rev-parse", $"HEAD:{path}").Trim();
            Assert.True(ObjectId.TryParse(output, out var id));

            return id;
        }
    }
}
