using Gitunia.Core.Staging;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Discard;

/// <summary>What a confirmed discard throws away, fixed when the dialog opened from the lists or the diff as they were
/// on screen. The dialog named exactly this; the effect never widens it.</summary>
public abstract record DiscardTarget
{
    private protected DiscardTarget()
    {
    }
}

/// <summary>All unstaged changes of one file; an untracked file or directory is deleted.</summary>
/// <param name="Entry">The status entry of the file.</param>
public sealed record DiscardFile(StatusEntry Entry) : DiscardTarget;

/// <summary>Several whole files (a folder or a Visual range of the Unstaged list), reported as a count.</summary>
/// <param name="Files">The files the dialog counted; conflicted, submodule and unreadable-name files are not in it.</param>
/// <param name="Skipped">How many picked files were left out.</param>
public sealed record DiscardFiles(IReadOnlyList<StatusEntry> Files, int Skipped) : DiscardTarget;

/// <summary>The hunk or the Visual lines of the Unstaged diff of one file.</summary>
/// <param name="Selection">The file and the changed lines taken from what was on screen.</param>
/// <param name="Move">Where the diff cursor goes once the diff is read again.</param>
public sealed record DiscardLines(StagingSelection Selection, DiffCursorMove Move) : DiscardTarget;
