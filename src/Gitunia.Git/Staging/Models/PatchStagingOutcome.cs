namespace Gitunia.Git.Staging.Models;

/// <summary>What <see cref="PatchStager"/> did.</summary>
public enum PatchStagingOutcome
{
    /// <summary>A patch of the chosen lines was applied to the index.</summary>
    Applied,

    /// <summary>The file has no hunks to pick from (binary, added, deleted, untracked or a name that is not valid UTF-8), so the whole file was staged or unstaged.</summary>
    WholeFile,

    /// <summary>The lines no longer match the file (it changed since the diff was read); nothing was changed.</summary>
    FileChanged,

    /// <summary>The choice holds no change (only unchanged lines); nothing was changed.</summary>
    NothingSelected,
}
