namespace Gitunia.Tui.Views;

/// <summary>The buttons in the title row of a working-tree file list.</summary>
public enum ListButtonKind
{
    /// <summary>The <c>stage all</c> (Unstaged) or <c>unstage all</c> (Staged) button.</summary>
    StageAll,

    /// <summary>The <c>view ▾</c> chip.</summary>
    View,

    /// <summary>The <c>resolve</c> chip, shown while the Unstaged cursor is on a conflicted file.</summary>
    Resolve,
}
