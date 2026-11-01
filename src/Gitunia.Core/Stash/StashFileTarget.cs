using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Core.Stash;

/// <summary>A file of a shown stash: what the restore acts on, as the user saw it.</summary>
/// <param name="Entry">The stash.</param>
/// <param name="File">The file and where in the stash commit it comes from.</param>
public sealed record StashFileTarget(StashEntry Entry, StashFileChange File);
