using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// The conflict editor overlay as the shell state holds it: the open editor (null while closed) and the bookkeeping of
/// the work around it. Changed only by <see cref="ConflictReducer"/>.
/// </summary>
/// <param name="Editor">The open editor, null while it is closed.</param>
/// <param name="Generation">Counts opens and closes; a result with another number belongs to an editor that is gone
/// and is dropped.</param>
/// <param name="Mode">The key mode the editor asked for last (Insert while typing, Visual while lines are
/// selected).</param>
/// <param name="Viewports">The pane rows at the screen size the terminal layer measured last, null when the screen is
/// too small to draw the editor.</param>
public sealed record ConflictSession(
    ConflictEditorState? Editor,
    long Generation,
    KeyMode Mode,
    ConflictViewports? Viewports)
{
    public static ConflictSession Empty { get; } = new(null, 0, KeyMode.Normal, null);

    public bool IsOpen => Editor is not null;
}
