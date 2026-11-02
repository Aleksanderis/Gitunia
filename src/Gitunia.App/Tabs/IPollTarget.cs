using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>What a <see cref="TabPoller"/> polls for: whether a poll may run now, and where a detected change goes.</summary>
public interface IPollTarget
{
    /// <summary>The tab is ready, idle and not refreshing or running an action.</summary>
    bool CanPoll { get; }

    /// <summary>Asks for a refresh of the given kinds.</summary>
    void Request(RepositoryChangeKinds kinds);
}
