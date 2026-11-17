namespace Gitunia.Core.Text;

/// <summary>
/// The closed vocabulary of hint labels for the keys that mean the same thing on every screen (Esc, Enter, j/k,
/// Ctrl-g, Ctrl-s): one word per action across the status bar and the overlay footers
/// (docs/internal/ui-consistency.md, section c2). A hint for one of those keys takes its label from here; a label
/// that names a feature (<c>file history</c>, <c>stage all</c>) is free text.
/// </summary>
public static class HintVerb
{
    /// <summary>Esc or q leaves an overlay or screen.</summary>
    public const string Close = "close";

    /// <summary>Esc steps out one level of a screen that is entered in steps.</summary>
    public const string Back = "back";

    /// <summary>Esc abandons what is being typed or picked, or ends a mode.</summary>
    public const string Cancel = "cancel";

    /// <summary>Esc empties a filter or a search.</summary>
    public const string Clear = "clear";

    /// <summary>Esc or Enter leaves editing and keeps what was typed.</summary>
    public const string Keep = "keep";

    /// <summary>Esc ends a repeat mode.</summary>
    public const string Done = "done";

    /// <summary>Enter opens the selected item (a diff, a screen, a setting).</summary>
    public const string Open = "open";

    /// <summary>Enter selects the item, or its commit in the graph.</summary>
    public const string Select = "select";

    /// <summary>Enter runs the highlighted command.</summary>
    public const string Run = "run";

    /// <summary>Enter or Ctrl-g or Ctrl-s stores the edit.</summary>
    public const string Save = "save";

    /// <summary>Enter edits the selected value.</summary>
    public const string Edit = "edit";

    /// <summary>Enter starts the operation the screen prepared.</summary>
    public const string Start = "start";

    /// <summary>Ctrl-g commits the message.</summary>
    public const string Commit = "commit";

    /// <summary>Enter adds a line in a multi-line editor.</summary>
    public const string NewLine = "new line";

    /// <summary>j/k move the highlighted row or the cursor line.</summary>
    public const string Move = "move";

    /// <summary>J/K and the wheel scroll content that has no highlighted row.</summary>
    public const string Scroll = "scroll";

    /// <summary>The hint of an open filter field.</summary>
    public const string TypeToFilter = "type to filter";

    /// <summary>The hint of a choice that a click outside also cancels.</summary>
    public const string CancelOrClickOutside = "or a click outside cancels";

    /// <summary>Every word, for the contract test.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Close, Back, Cancel, Clear, Keep, Done, Open, Select, Run, Save, Edit, Start, Commit, NewLine, Move, Scroll,
        TypeToFilter, CancelOrClickOutside,
    ];
}
