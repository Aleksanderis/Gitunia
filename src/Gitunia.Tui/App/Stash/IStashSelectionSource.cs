using Gitunia.Core.Stash;

namespace Gitunia.Tui.App.Stash;

/// <summary>Says what the stash actions act on right now, as the user sees it.</summary>
public interface IStashSelectionSource
{
    /// <summary>The stash commit of the sidebar row under the cursor of the active tab; null when it is no stash row.</summary>
    string? CursorStashCommit();

    /// <summary>The file of the shown stash under the cursor of the detail pane; null when there is none.</summary>
    StashFileTarget? FileUnderCursor();
}
