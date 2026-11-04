namespace Gitunia.Core.Visual;

/// <summary>Why a selection is copied, which decides what happens to it afterwards.</summary>
public enum VisualCopyMode
{
    /// <summary><c>y</c> or the palette action: the copy ends the selection.</summary>
    Yank,

    /// <summary>
    /// Copy on select (a mouse release or a double or triple click): the selection stays, so <c>y</c> still works.
    /// </summary>
    OnSelect,

    /// <summary>
    /// <c>Ctrl-c</c> on a selection: the selection stays and the status bar says that a second press quits.
    /// </summary>
    CtrlC,
}
