namespace Gitunia.Core.Editing;

/// <summary>
/// The policy of every host of a text editor, in one place. Esc keeps the text everywhere (decision 2 of the UI
/// consistency plan). The conflict result keeps its own undo of whole changes (`ConflictEditorState.Undo`).
/// </summary>
public static class EditorHosts
{
    /// <summary>The commit message box.</summary>
    public static EditorHostPolicy CommitBox { get; } = new(HasUndo: true, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>The message of a reword in the interactive rebase.</summary>
    public static EditorHostPolicy Reword { get; } = new(HasUndo: true, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>
    /// The result pane of the conflict editor: copy and cut go through <see cref="EditorHostKeys.CopySelection"/>; undo
    /// is its own, by whole changes, not the editor's typing steps.
    /// </summary>
    public static EditorHostPolicy ConflictResult { get; } = new(HasUndo: false, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>The one-line input of the settings screen (a number or a folder path).</summary>
    public static EditorHostPolicy SettingsInput { get; } = new(HasUndo: false, HasClipboard: false, DraftPolicy.Keep);
}
