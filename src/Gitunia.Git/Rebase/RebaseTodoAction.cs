namespace Gitunia.Git.Rebase;

/// <summary>What an interactive rebase does with one commit of the range.</summary>
public enum RebaseTodoAction
{
    /// <summary>Keep the commit as it is.</summary>
    Pick,

    /// <summary>Keep the commit with a new message.</summary>
    Reword,

    /// <summary>Meld the commit into the one before it and keep both messages.</summary>
    Squash,

    /// <summary>Meld the commit into the one before it and keep only that one's message.</summary>
    Fixup,

    /// <summary>Remove the commit.</summary>
    Drop,

    /// <summary>
    /// <c>label &lt;name&gt;</c> (<c>--rebase-merges</c>): names the commit HEAD is at, for a later reset or merge. The
    /// name is carried in <see cref="RebaseTodoEntry.Commit"/>. Not a commit, not editable.
    /// </summary>
    Label,

    /// <summary>
    /// <c>reset &lt;name&gt;</c> (<c>--rebase-merges</c>): moves HEAD to a label or commit, where a new line of commits
    /// starts. The name is carried in <see cref="RebaseTodoEntry.Commit"/>. Not a commit, not editable.
    /// </summary>
    Reset,

    /// <summary>
    /// <c>merge -C &lt;commit&gt; &lt;labels&gt;</c> (<c>--rebase-merges</c>): recreates the merge commit
    /// <see cref="RebaseTodoEntry.Commit"/> from the labels in <see cref="RebaseTodoEntry.Argument"/>. Kept as it is.
    /// </summary>
    Merge,
}
