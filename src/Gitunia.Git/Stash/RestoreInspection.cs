using Gitunia.Git.Stash.Models;

namespace Gitunia.Git.Stash;

/// <summary>What looking at a file of a stash found, and the revision its content is read from.</summary>
/// <param name="State">What a restore would do.</param>
/// <param name="Source">The revision holding the stashed file: the stash commit or its third parent.</param>
internal sealed record RestoreInspection(StashRestoreState State, string Source);
