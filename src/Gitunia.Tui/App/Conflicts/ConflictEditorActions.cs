using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Conflicts;

/// <summary>Gives the actions to the conflict editor and tells it whether another overlay covers it.</summary>
/// <param name="editor">The conflict editor.</param>
/// <param name="cover">Says whether another overlay covers the editor.</param>
public sealed class ConflictEditorActions(ConflictEditorInput editor, IConflictCover cover) : IActionHandler
{
    public bool TryExecute(ActionId action, int? count) =>
        editor.TryExecute(action, count, cover.IsOtherThanConflictEditorOpen());
}
