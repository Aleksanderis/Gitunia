namespace Gitunia.Git.Trees.Models;

/// <summary>What a path in a commit's tree is, from its mode and object type.</summary>
public enum TreeEntryKind
{
    /// <summary>A regular file.</summary>
    File,

    /// <summary>A regular file with the executable bit.</summary>
    Executable,

    /// <summary>A symbolic link (the blob holds the target).</summary>
    Symlink,

    /// <summary>A submodule: a commit of another repository, recorded by its object id only.</summary>
    Submodule,
}
