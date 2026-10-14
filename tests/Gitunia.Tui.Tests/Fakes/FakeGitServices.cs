using Gitunia.Git.Repositories;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Graph;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The services of the app with every reader replaced by a fake, wired like <see cref="GitServices.Create"/>; only the repository discovery stays real (nothing under test calls it).</summary>
public sealed class FakeGitServices
{
    public FakeGitServices()
    {
        var graphLoader = new GraphLoader(CommitLog);
        var refresher = new RepositoryRefresher(Refs, Status, new FakeRepositoryOperationDetector());
        Services = TestGit.Services with
        {
            CommitLog = CommitLog,
            CommitDetails = CommitDetails,
            Diff = Diff,
            Refs = Refs,
            Status = Status,
            Stashes = Stashes,
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

    /// <summary>The services to hand to the code under test.</summary>
    public GitServices Services { get; }
}
