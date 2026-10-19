namespace Gitunia.Core.FileTree;

/// <summary>What a row of the file tree is.</summary>
public enum FileTreeRowKind
{
    /// <summary>A directory, or a chain of single-child directories folded into one row.</summary>
    Directory,

    /// <summary>A regular file.</summary>
    File,

    /// <summary>A regular file with the executable bit.</summary>
    Executable,

    /// <summary>A symbolic link.</summary>
    Symlink,

    /// <summary>A submodule (a commit of another repository, not a directory of files).</summary>
    Submodule,
}
