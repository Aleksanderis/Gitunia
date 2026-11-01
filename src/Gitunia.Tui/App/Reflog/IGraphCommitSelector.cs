using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Reflog;

/// <summary>Moves the graph cursor of a tab to a commit.</summary>
public interface IGraphCommitSelector
{
    /// <summary>Selects the commit; returns why it could not, empty when it did.</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="hash">The commit.</param>
    string SelectCommit(TabId tabId, string hash);
}
