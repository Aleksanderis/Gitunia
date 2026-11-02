using Gitunia.App.Loading.Graph;
using Gitunia.App.Status;
using Gitunia.App.Tabs.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Config;
using Gitunia.Git.Operations;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>The git readers a <see cref="TabRefresher"/> re-reads a repository with.</summary>
public sealed record TabRefreshServices(
    IRepositoryRefresher Refresher,
    IStashReader Stashes,
    IncrementalGraphLoader GraphLoader,
    IOperationInfoReader? Operations = null,
    IWorkingTreeDirectoryLister? DirectoryLister = null,
    IGitConfigReader? Config = null);

/// <summary>The tab a <see cref="TabRefresher"/> keeps current and where it reports to.</summary>
public sealed record TabContext(
    Dispatcher Host,
    IGraphSession Session,
    RepositoryLocation Location,
    StatusReporter Statuses,
    IUiInvoker Ui);

/// <summary>How a <see cref="TabRefresher"/> notices outside changes: file watchers or polling.</summary>
public sealed record TabWatching(
    WatchBudget Budget,
    RepositoryWatcherOptions Options,
    PollOptions? Poll = null,
    ITabBusySource? Busy = null);

internal sealed class NeverBusy : ITabBusySource
{
    public static readonly NeverBusy Instance = new();

    public bool IsBusy(TabId tab) => false;
}
