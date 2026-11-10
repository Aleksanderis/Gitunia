using Gitunia.Core.Keymap;
namespace Gitunia.Core.Conflicts;

/// <summary>The status messages of the conflict editor flow.</summary>
public static class ConflictNotices
{
    /// <summary>A second operation of the tab was asked for while one runs.</summary>
    public const string Busy = "Busy: another operation of this tab is still running";

    /// <summary>The screen has no room for the confirm dialog.</summary>
    public const string TooSmall = "The screen is too small to ask: nothing was saved";

    /// <summary>A save without any pick or edit.</summary>
    public const string NothingPicked = "Nothing is picked or edited yet: nothing to save";

    /// <summary>The base was taken from a file without a common ancestor.</summary>
    public const string NoBase = "This conflict has no common ancestor: set merge.conflictStyle=diff3, then R";

    /// <summary>The take key was pressed with the cursor in the result.</summary>
    public const string NotInVersion = "Move to the base, ours or theirs pane to take a version (Tab)";

    /// <summary>A Visual selection holds no line of the conflict.</summary>
    public const string NoLines = "The selection holds no line of the current conflict: nothing was taken";

    /// <summary>Untake on a conflict nobody decided.</summary>
    public const string NothingToUntake = "This conflict is unresolved: nothing to untake";

    /// <summary>Untake of a selection that holds no line of the result.</summary>
    public const string NoLinesToUntake =
        "None of the selected lines is in this conflict's result: nothing was untaken";

    /// <summary>The selection key was pressed with the cursor in the result.</summary>
    public const string SelectInVersion = "Lines are selected in the base, ours or theirs pane (Tab)";

    /// <summary>The editor key was pressed and no file is conflicted.</summary>
    public const string NoConflicts = "No conflicts to resolve: no file is conflicted";

    /// <summary>The editor key was pressed before the working tree was read.</summary>
    public const string NotLoaded = "The working tree is not loaded yet: try again in a moment";

    /// <summary>The tab was closed while the file was read.</summary>
    public const string Changed = "The tab changed while the file was read: the conflict editor was not opened";

    /// <summary>Another view opened while the file was read.</summary>
    public const string Covered = "Another view opened while the file was read: the conflict editor was not opened";

    /// <summary>The file differs from what the editor was opened on.</summary>
    public const string FileChanged = "The file changed on disk since it was opened: nothing was saved";

    /// <summary>The hint for a conflict the editor refuses.</summary>
    public static readonly string MergetoolHint = $"use {KeyTokens.Of(ActionIds.ToolMergetool, "the mergetool")} instead";

    /// <summary>The status line while typing without a conflict under the cursor.</summary>
    public const string TypingInResult = "Typing in the result: Esc or Ctrl-g keeps the text";

    /// <summary>A copy of a line on a filler row or of a Visual selection without text.</summary>
    public const string NothingToCopy = "Nothing to copy: no text on this line";

    /// <summary>A copy key with no text selected in the result.</summary>
    public const string NothingSelected = "Nothing is selected: drag with the mouse or Shift with the arrows first";

    /// <summary>A put before anything was copied.</summary>
    public const string NothingToPut = "Nothing to put yet: copy lines first (yy, Visual y, Alt-w)";

    /// <summary>What a copied line is called in the status message.</summary>
    public const string LineDescription = "line";

    /// <summary>What copied selected text is called in the status message.</summary>
    public const string SelectionDescription = "selected text";

    /// <summary>A put key with the cursor in a version pane.</summary>
    public const string PutInResult = "Put works in the result pane (Tab): move there first";
}
