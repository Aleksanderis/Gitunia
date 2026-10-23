namespace Gitunia.Core.Conflicts;

/// <summary>How the user resolved one conflict hunk.</summary>
public enum ConflictPick
{
    /// <summary>Not resolved yet: saving keeps the hunk's conflict markers.</summary>
    None,

    /// <summary>The current side's lines.</summary>
    Ours,

    /// <summary>The other side's lines.</summary>
    Theirs,

    /// <summary>Both sides, ours first.</summary>
    Both,
}
