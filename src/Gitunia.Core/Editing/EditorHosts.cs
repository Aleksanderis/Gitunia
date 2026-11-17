namespace Gitunia.Core.Editing;

/// <summary>
/// The policy of every host of a text editor, in one place. Esc keeps the text everywhere (decision 2 of the UI
/// consistency plan); the conflict result has its own undo of whole changes, so the editor's undo points are off there.
/// </summary>
public static class EditorHosts
{
    /// <summary>The commit message box.</summary>
    public static EditorHostPolicy CommitBox { get; } = new(HasUndo: true, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>The message of a reword in the interactive rebase.</summary>
    public static EditorHostPolicy Reword { get; } = new(HasUndo: true, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>The result buffer of the conflict editor (its undo is per change, outside the editor).</summary>
    public static EditorHostPolicy ConflictResult { get; } = new(HasUndo: false, HasClipboard: true, DraftPolicy.Keep);

    /// <summary>The one-line input of the settings screen (a number or a folder path).</summary>
    public static EditorHostPolicy SettingsInput { get; } = new(HasUndo: false, HasClipboard: false, DraftPolicy.Keep);
}
