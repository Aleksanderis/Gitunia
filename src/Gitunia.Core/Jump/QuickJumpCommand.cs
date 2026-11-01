using System.Collections.Immutable;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Jump;

/// <summary>A command of the quick jump prompt (<c>&lt;space&gt;j</c>).</summary>
public abstract record QuickJumpCommand : AppCommand
{
    private protected QuickJumpCommand()
    {
    }
}

/// <summary>
/// The jump key was pressed. The view says whether the focused list has a target and why it may be unavailable, as
/// it alone knows its rows.
/// </summary>
/// <param name="TabId">The active tab, null when none is open.</param>
/// <param name="Pane">The focused pane.</param>
/// <param name="HasTarget">The focused pane has a list that can be jumped in.</param>
/// <param name="UnavailableReason">Why the list cannot be jumped in right now, null when it can.</param>
public sealed record QuickJumpOpenRequested(TabId? TabId, PaneKind Pane, bool HasTarget, string? UnavailableReason)
    : QuickJumpCommand;

/// <summary>A key was typed in the prompt. <paramref name="Labels"/> are those of the text typed before the key.</summary>
/// <param name="Key">The key.</param>
/// <param name="Labels">The labels the list shows for the current text.</param>
public sealed record QuickJumpKeyPressed(KeyEvent Key, ImmutableArray<JumpLabel> Labels) : QuickJumpCommand;

/// <summary>Enter was pressed: jump to the nearest match, which is the first label.</summary>
/// <param name="Labels">The labels the list shows for the current text.</param>
public sealed record QuickJumpAccepted(ImmutableArray<JumpLabel> Labels) : QuickJumpCommand;

/// <summary>The prompt ends without a jump (Esc, or the tab or pane changed under it).</summary>
public sealed record QuickJumpClosed : QuickJumpCommand;
