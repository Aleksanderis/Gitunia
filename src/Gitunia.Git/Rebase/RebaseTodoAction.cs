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
}
