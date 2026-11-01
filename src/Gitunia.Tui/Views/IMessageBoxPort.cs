using Gitunia.Core.Editing;

namespace Gitunia.Tui.Views;

/// <summary>What the commit message box and its option row read to draw: the facts of the active tab, read on each draw.</summary>
public interface IMessageBoxPort
{
    /// <summary>The commit draft of the active tab, null when it has none.</summary>
    CommitDraft? Draft { get; }

    /// <summary>True while the message box shows its text cursor (Insert mode, and the box's Normal mode).</summary>
    bool ShowsCursor { get; }

    /// <summary>True while a commit of the active tab runs, so the message box is read-only.</summary>
    bool IsCommitting { get; }
}
