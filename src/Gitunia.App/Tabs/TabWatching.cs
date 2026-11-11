using Gitunia.App.Tabs.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>How a <see cref="TabRefresher"/> notices outside changes: file watchers or polling.</summary>
public sealed record TabWatching(
    WatchBudget Budget,
    RepositoryWatcherOptions Options,
    PollOptions? Poll = null,
    ITabBusySource? Busy = null,
    TimeProvider? Time = null);

internal sealed class NeverBusy : ITabBusySource
{
    public static readonly NeverBusy Instance = new();

    public bool IsBusy(TabId tab) => false;
}
