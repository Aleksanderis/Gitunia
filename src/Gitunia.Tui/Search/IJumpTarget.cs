using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Search;

/// <summary>
/// One list a quick jump works on (the graph, a diff, a file list): it works out the labels for the typed text, shows them
/// while the prompt is open and moves the cursor when one is chosen. The prompt (<see cref="QuickJumpController"/>) knows nothing
/// else about the list, so a new list only needs a target, and anything that labels rows for a key press can reuse the prompt.
/// A target belongs to one tab and one open prompt; the prompt asks for a new one each time it opens.
/// </summary>
public interface IJumpTarget
{
    /// <summary>The pane the target belongs to; the prompt closes when focus moves to another one.</summary>
    PaneKind Pane { get; }

    /// <summary>Why the list cannot be jumped in right now (for the status bar), null when it can.</summary>
    string? UnavailableReason { get; }

    /// <summary>The targets of <paramref name="text"/> among the rows in view, nearest the cursor first.</summary>
    /// <param name="text">The typed text.</param>
    IReadOnlyList<JumpLabel> Labels(string text);

    /// <summary>Starts drawing the labels of <paramref name="text"/> over the rows, or stops when it is null.</summary>
    /// <param name="text">The typed text, null for none.</param>
    void ShowLabels(string? text);

    /// <summary>Moves the cursor to a row and scrolls it into view.</summary>
    /// <param name="row">The row of the target's own rows.</param>
    void JumpTo(int row);
}
