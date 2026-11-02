namespace Gitunia.Tui.App.Shell;

/// <summary>The groups of shell effects the terminal layer performs.</summary>
/// <param name="Cursor">Moves list cursors.</param>
/// <param name="Stash">The stash view.</param>
/// <param name="Tools">External programs.</param>
/// <param name="Copy">Copying and the terminal retry.</param>
/// <param name="Session">The session, quitting, options and themes.</param>
/// <param name="Flow">Reflog entries, resolved conflicts, file content.</param>
public sealed record ShellEffectParts(
    CursorEffects Cursor,
    StashEffects Stash,
    ToolEffects Tools,
    CopyEffects Copy,
    SessionEffects Session,
    FlowEffects Flow);
