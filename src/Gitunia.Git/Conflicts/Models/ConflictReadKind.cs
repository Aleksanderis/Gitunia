namespace Gitunia.Git.Conflicts.Models;

/// <summary>Whether a conflicted path can be resolved hunk by hunk.</summary>
public enum ConflictReadKind
{
    /// <summary>Both sides changed a text file: <see cref="ConflictReadResult.Parts"/> holds the hunks.</summary>
    Text,

    /// <summary>Not editable here (add/add, delete/modify, binary, odd markers): use the mergetool.</summary>
    Unsupported,
}
