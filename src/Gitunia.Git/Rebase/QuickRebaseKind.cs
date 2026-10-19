namespace Gitunia.Git.Rebase;

/// <summary>A one-step history edit of a single commit, run through the interactive rebase engine.</summary>
public enum QuickRebaseKind
{
    /// <summary>Meld the commit into the one before it and keep only that one's message.</summary>
    Fixup,

    /// <summary>Change the message of the commit.</summary>
    Reword,

    /// <summary>Remove the commit from the history of the current branch.</summary>
    Drop,
}
