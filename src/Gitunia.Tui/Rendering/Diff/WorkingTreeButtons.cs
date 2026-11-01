namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Which staging buttons the title row of the Changes view diff carries (none for the history diff).</summary>
public enum WorkingTreeButtons
{
    None,

    /// <summary>The Unstaged side: <c>stage</c> and <c>discard</c>.</summary>
    Unstaged,

    /// <summary>The Staged side: <c>unstage</c>.</summary>
    Staged,
}
