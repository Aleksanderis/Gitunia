namespace Gitunia.Git.Conflicts.Models;

/// <summary>Whether a conflicted path can be resolved in the conflict editor.</summary>
public enum ConflictReadKind
{
    /// <summary>Both sides changed or added a text file (add/add has an empty base): <see cref="ConflictReadResult.Parts"/> holds the hunks.</summary>
    Text,

    /// <summary>Not editable here (delete/modify, binary, odd markers): use the mergetool.</summary>
    Unsupported,
}
