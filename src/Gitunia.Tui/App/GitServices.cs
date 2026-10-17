using Gitunia.Git.Branches;
using Gitunia.Git.Merge;
using Gitunia.Git.Rebase;
using Gitunia.Git.Pick;
using Gitunia.Git.Reset;
using Gitunia.Git.Commits;
using Gitunia.Git.Diff;
using Gitunia.Git.Discard;
using Gitunia.Platform.FileSystem;
using Gitunia.Git.Execution;
using Gitunia.Git.Operations;
using Gitunia.Git.Reflog;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Staging;
using Gitunia.Git.Stash;
using Gitunia.Git.Tags;
using Gitunia.Git.Worktrees;
using Gitunia.Git.Status;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Graph;

namespace Gitunia.Tui.App;

/// <summary>
/// The git readers and the loaders built on them, created once by the composition root and handed to the parts of the app that
/// read a repository. Consumers see interfaces, so tests replace git with fakes (use <c>with</c> to swap one part).
/// </summary>
/// <param name="Discovery">Resolves a path to its repository.</param>
/// <param name="CommitLog">Reads the commit log.</param>
/// <param name="CommitDetails">Reads one commit's details.</param>
/// <param name="Diff">Reads diffs.</param>
/// <param name="Refs">Reads HEAD, refs and worktrees.</param>
/// <param name="Status">Reads the working tree status.</param>
/// <param name="IndexWriter">Stages and unstages files.</param>
/// <param name="Discarder">Throws working-tree changes away (always behind a confirm dialog).</param>
/// <param name="CommitWriter">Creates commits.</param>
/// <param name="Branches">Creates, switches, renames and deletes branches.</param>
/// <param name="Merge">Merges a branch or commit into the current branch.</param>
/// <param name="Pick">Cherry-picks or reverts commits onto the current branch.</param>
/// <param name="PickCommits">Reads the commits a cherry-pick or revert acts on.</param>
/// <param name="Reset">Moves the current branch to a commit (soft, mixed, hard).</param>
/// <param name="Rebase">Rebases the current branch onto a commit (non-interactive).</param>
/// <param name="Reflog">Reads the reflog of HEAD or of a branch.</param>
/// <param name="Stashes">Reads the stash stack.</param>
/// <param name="StashWriter">Pushes, applies, pops and drops stashes.</param>
/// <param name="OperationWriter">Continues, aborts and skips a stopped merge, rebase, cherry-pick or revert.</param>
/// <param name="OperationInfo">Reads the details of the stopped operation for the banner.</param>
/// <param name="Refresher">Recomputes a tab's snapshot.</param>
/// <param name="GraphLoader">Streams the graph rows.</param>
/// <param name="IncrementalGraphLoader">Reloads a graph after a change.</param>
/// <param name="RepositoryLoader">Loads everything a freshly opened tab shows.</param>
public sealed record GitServices(
    RepositoryDiscovery Discovery,
    ICommitLogReader CommitLog,
    ICommitDetailsReader CommitDetails,
    IDiffReader Diff,
    IRefReader Refs,
    IStatusReader Status,
    IIndexWriter IndexWriter,
    IWorkingTreeDiscarder Discarder,
    ICommitWriter CommitWriter,
    IBranchWriter Branches,
    ITagWriter Tags,
    IMergeWriter Merge,
    IPickWriter Pick,
    IPickCommitReader PickCommits,
    IResetWriter Reset,
    IRebaseWriter Rebase,
    IReflogReader Reflog,
    IStashReader Stashes,
    IStashWriter StashWriter,
    IWorktreeWriter Worktrees,
    IOperationWriter OperationWriter,
    IOperationInfoReader OperationInfo,
    IRepositoryRefresher Refresher,
    GraphLoader GraphLoader,
    IncrementalGraphLoader IncrementalGraphLoader,
    RepositoryLoader RepositoryLoader)
{
    /// <summary>The real services: every reader runs the git CLI through <paramref name="runner"/>. Hand-wired, no container.</summary>
    public static GitServices Create(GitRunner runner)
    {
        var commitLog = new CommitLogReader(runner);
        var refs = new RefReader(runner);
        var status = new StatusReader(runner);
        var stashes = new StashReader(runner);
        var graphLoader = new GraphLoader(commitLog);
        var operationDetector = new RepositoryOperationDetector();
        var operationRunner = new OperationRunner(runner, operationDetector);
        return new GitServices(
            new RepositoryDiscovery(runner),
            commitLog,
            new CommitDetailsReader(runner),
            new DiffReader(runner),
            refs,
            status,
            new IndexWriter(runner),
            new WorkingTreeDiscarder(runner, ByteExactFileSystems.ForCurrentOs()),
            new CommitWriter(runner),
            new BranchWriter(runner),
            new TagWriter(runner),
            new MergeWriter(runner, operationRunner),
            new PickWriter(runner, operationRunner),
            new PickCommitReader(runner),
            new ResetWriter(runner),
            new RebaseWriter(operationRunner),
            new ReflogReader(runner),
            stashes,
            new StashWriter(runner),
            new WorktreeWriter(runner),
            new OperationWriter(operationRunner),
            new OperationInfoReader(),
            new RepositoryRefresher(refs, status, operationDetector),
            graphLoader,
            new IncrementalGraphLoader(commitLog, graphLoader),
            new RepositoryLoader(refs, status, graphLoader, new SidebarDataLoader(refs, stashes)));
    }
}
