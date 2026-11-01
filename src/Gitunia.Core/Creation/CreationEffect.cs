using System.Collections.Immutable;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Creation;

/// <summary>Work for the clone and init flow, run off the UI thread.</summary>
public abstract record CreationEffect : AppEffect
{
    private protected CreationEffect()
    {
    }
}

/// <summary>Find the folder a clone goes into by default. Posts <see cref="CloneParentResolved"/>.</summary>
/// <param name="Source">The resolved clone source, echoed in the result.</param>
/// <param name="TypedPath">The path typed in the picker, null when the picker holds a search.</param>
/// <param name="EndsWithSeparator">The typed path ends with a separator: it names a folder.</param>
/// <param name="ScanRoots">The folders the picker scans, null for the conventional ones.</param>
/// <param name="HomeDirectory">The home directory.</param>
public sealed record ResolveCloneParentEffect(
    string Source,
    string? TypedPath,
    bool EndsWithSeparator,
    ImmutableArray<string>? ScanRoots,
    string HomeDirectory) : CreationEffect;

/// <summary>Clone a repository. Posts <see cref="CloneProgressed"/> and then <see cref="CloneSucceeded"/>,
/// <see cref="CloneFailed"/> or <see cref="CloneCancelled"/>.</summary>
/// <param name="Request">The source and the target directory.</param>
public sealed record CloneRepositoryEffect(CloneRequest Request) : CreationEffect;

/// <summary>Stop the running clone; its result is <see cref="CloneCancelled"/>.</summary>
public sealed record CancelCloneEffect : CreationEffect;

/// <summary>Run <c>git init</c> in a directory (created when missing). Posts <see cref="InitSucceeded"/> or
/// <see cref="InitFailed"/>.</summary>
/// <param name="Directory">The directory.</param>
public sealed record InitRepositoryEffect(string Directory) : CreationEffect;
