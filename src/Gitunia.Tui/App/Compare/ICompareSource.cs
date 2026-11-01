using Gitunia.Git;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Compare;

/// <summary>What the comparison keys read of the shell: the session with the loaded commits and the graph cursor.</summary>
public interface ICompareSource
{
    /// <summary>The active tab's session, null when there is none.</summary>
    TerminalTabSession? ActiveSession();

    /// <summary>The commit under the graph cursor, null when none.</summary>
    ObjectId? SelectedCommitId();
}
