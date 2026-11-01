namespace Gitunia.Tui.App.Conflicts;

/// <summary>Says whether an overlay other than the conflict editor is open (its Confirm above all) and covers it.</summary>
public interface IConflictCover
{
    /// <summary>True while the rebase editor, a dialog, box, prompt or other overlay is open over the editor.</summary>
    bool IsOtherThanConflictEditorOpen();
}
