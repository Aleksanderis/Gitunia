using Gitunia.App;
using Gitunia.App.Loading;
using Gitunia.App.Loading.Graph;
using Gitunia.Git.Repositories;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The services of the app with every reader replaced by a fake, wired by hand like <see cref="GitServices.Create"/> (add a new part there to here too); only the repository discovery stays real (nothing under test calls it).</summary>
public sealed class FakeGitServices
{
    public FakeGitServices()
    {
        var graphLoader = new GraphLoader(CommitLog);
        var refresher = new RepositoryRefresher(Refs, Status, new RepositoryOperationDetector());
        Services = TestGit.Services with
        {
            CommitLog = CommitLog,
            CommitDetails = CommitDetails,
            Diff = Diff,
            Refs = Refs,
            Status = Status,
            Stashes = Stashes,
            Config = Config,
            Refresher = refresher,
            GraphLoader = graphLoader,
            IncrementalGraphLoader = new IncrementalGraphLoader(CommitLog, graphLoader),
            RepositoryLoader = new RepositoryLoader(Refs, Status, graphLoader, new SidebarDataLoader(Refs, Stashes)),
        };
    }

    public FakeCommitLogReader CommitLog { get; } = new();

    public FakeCommitDetailsReader CommitDetails { get; } = new();

    public FakeDiffReader Diff { get; } = new();

    public FakeRefReader Refs { get; } = new();

    public FakeStatusReader Status { get; } = new();

    public FakeStashReader Stashes { get; } = new();

    public FakeGitConfigReader Config { get; } = new();

    /// <summary>The services to hand to the code under test.</summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global : Fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    public GitServices Services { get; }
}
