namespace Gitunia.Tui.App.Branches;

/// <summary>Says what the branch, tag and push actions act on right now: a sidebar row or the graph commit.</summary>
public interface IBranchSelectionSource
{
    /// <summary>The selection as the user sees it at this moment.</summary>
    BranchSelection Current();
}
