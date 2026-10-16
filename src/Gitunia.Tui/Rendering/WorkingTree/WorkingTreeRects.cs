using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>Where the panes of the working-tree view sit (content rectangles; a pane that is not shown is empty).</summary>
/// <param name="Unstaged">The Unstaged file list.</param>
/// <param name="Staged">The Staged file list.</param>
/// <param name="Diff">The diff of the selected file (a placeholder until the diff pane arrives).</param>
/// <param name="Message">The commit message box: the editor above the option row.</param>
public sealed record WorkingTreeRects(PaneRect Unstaged, PaneRect Staged, PaneRect Diff, PaneRect Message)
{
    /// <summary>The rows of the message box that hold the text; the last row is the option row.</summary>
    public PaneRect MessageEditor => Message.IsEmpty
        ? Message
        : new PaneRect(Message.X, Message.Y, Message.Width, Math.Max(0, Message.Height - 1));

    /// <summary>The last row of the message box: the options and the Commit button.</summary>
    public PaneRect MessageOptions => Message.IsEmpty
        ? Message
        : new PaneRect(Message.X, Message.Y + Message.Height - 1, Message.Width, 1);
}
