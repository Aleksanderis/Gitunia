namespace Gitunia.Tui.App.Rebase;

/// <summary>Says which commit is selected in the graph, whichever pane has focus.</summary>
public interface IGraphSelectionSource
{
    /// <summary>The hash of the selected graph commit, null when none.</summary>
    string? SelectedCommit();
}
