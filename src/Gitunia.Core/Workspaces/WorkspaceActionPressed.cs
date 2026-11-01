using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Workspaces;

/// <summary>A bound action the workspace level owns (tabs, pinning, the repository picker). Only sent for an action
/// <see cref="WorkspaceActionReducer.Owns"/> says is owned, or while a notice or the close warning is pending.</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="ActiveTabHasUnsentMessage">The active tab holds a commit message that was not committed.</param>
/// <param name="AnyUnsentMessage">Any tab holds one (for the quit of the picker).</param>
/// <param name="Now">The time, for the picker's rescan interval.</param>
public sealed record WorkspaceActionPressed(
    ActionId Action,
    int? Count,
    bool ActiveTabHasUnsentMessage,
    bool AnyUnsentMessage,
    DateTimeOffset Now) : AppCommand;
