using Gitunia.App.Loading.Graph;
using Gitunia.Git.Config;
using Gitunia.Git.Operations;
using Gitunia.Git.Repositories;
using Gitunia.Git.Stash;
using Gitunia.Git.Watching;

namespace Gitunia.App.Tabs;

/// <summary>The git readers a <see cref="TabRefresher"/> re-reads a repository with.</summary>
public sealed record TabRefreshServices(
    IRepositoryRefresher Refresher,
    IStashReader Stashes,
    IncrementalGraphLoader GraphLoader,
    IOperationInfoReader? Operations = null,
    IWorkingTreeDirectoryLister? DirectoryLister = null,
    IGitConfigReader? Config = null);
