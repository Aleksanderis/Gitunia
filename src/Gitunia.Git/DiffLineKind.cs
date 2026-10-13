namespace Gitunia.Git;

/// <summary>What a line of a hunk does to the file.</summary>
public enum DiffLineKind
{
    Context,
    Added,
    Removed,
}
