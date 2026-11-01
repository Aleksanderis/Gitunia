using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces;

/// <summary>What the workspace-level actions (tabs, pinning) keep between presses.</summary>
/// <param name="CloseArmedFor">The tab whose first close only warned about an unsent commit message; the same close
/// again right after (nothing else in between) closes it and drops the draft.</param>
/// <param name="Notice">A short notice for the status bar from the last action, empty when none.</param>
public sealed record WorkspaceActionSession(TabId? CloseArmedFor, string Notice)
{
    public static WorkspaceActionSession Empty { get; } = new(null, string.Empty);
}
