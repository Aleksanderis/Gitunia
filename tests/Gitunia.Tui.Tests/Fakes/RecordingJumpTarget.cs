using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Search;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>A jump target with fixed labels that records what was shown and jumped to.</summary>
/// <param name="pane">The pane the target belongs to.</param>
/// <param name="labels">The labels to offer.</param>
/// <param name="shown">Receives the text of every <see cref="ShowLabels"/> call.</param>
/// <param name="jumps">Receives every row jumped to.</param>
/// <param name="unavailableReason">Why the target is unavailable, null for available.</param>
public sealed class RecordingJumpTarget(
    PaneKind pane,
    IReadOnlyList<JumpLabel> labels,
    List<string?> shown,
    List<int> jumps,
    string? unavailableReason = null) : IJumpTarget
{
    public PaneKind Pane => pane;

    public string? UnavailableReason => unavailableReason;

    public IReadOnlyList<JumpLabel> Labels(string text) => labels;

    public void ShowLabels(string? text) => shown.Add(text);

    public void JumpTo(int row) => jumps.Add(row);
}
