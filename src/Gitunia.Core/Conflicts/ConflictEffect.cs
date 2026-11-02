using Gitunia.Core.Status;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>Work for the conflict editor. Each posts one result <see cref="ConflictCommand"/>; none changes
/// state.</summary>
public abstract record ConflictEffect : OperationEffect
{
    private protected ConflictEffect(TabId tabId, RepositoryLocation location, string path)
        : base(tabId, RunningOperationKind.Conflicts)
    {
        Location = location;
        Path = path;
    }

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }

    /// <summary>The conflicted file, relative to the working tree root.</summary>
    public string Path { get; }

    /// <inheritdoc />
    public override AppCommand FailedWith(string text) => new ConflictOperationFailed(TabId, text, StatusSeverity.Error);
}

/// <summary>Read the conflicted file into its parts. Posts <see cref="ConflictFileRead"/>.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the file is.</param>
/// <param name="Path">The file.</param>
/// <param name="Generation">The open number; the result echoes it.</param>
/// <param name="Conflict">The kind of conflict the status reported.</param>
public sealed record ReadConflictEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Path,
    long Generation,
    ConflictKind Conflict) : ConflictEffect(TabId, Location, Path);

/// <summary>
/// Write the result into the working tree file, unless the file changed since the editor read it. Posts
/// <see cref="ConflictSaved"/> or <see cref="ConflictOperationFailed"/>.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the file is.</param>
/// <param name="Path">The file.</param>
/// <param name="OriginalText">The file as the editor read it; the file is read again and must still match.</param>
/// <param name="Text">The whole new content, fixed when the user confirmed.</param>
/// <param name="Message">The status line after a successful write.</param>
/// <param name="IsComplete">Whether every conflict was settled, which decides the "mark resolved" step.</param>
public sealed record WriteConflictEffect(
    TabId TabId,
    RepositoryLocation Location,
    string Path,
    string OriginalText,
    string Text,
    string Message,
    bool IsComplete) : ConflictEffect(TabId, Location, Path);

/// <summary>
/// Re-create the conflict of the file and read it again. Posts <see cref="ConflictRecreated"/> or
/// <see cref="ConflictOperationFailed"/>.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the file is.</param>
/// <param name="Path">The file.</param>
public sealed record RecreateConflictEffect(TabId TabId, RepositoryLocation Location, string Path)
    : ConflictEffect(TabId, Location, Path);

/// <summary>
/// Mark a saved, fully resolved file as resolved (stage it). Staging lives in the terminal layer until it migrates,
/// so the shell carries it out.
/// </summary>
/// <param name="TabId">The tab.</param>
/// <param name="Path">The file.</param>
public sealed record StageResolvedConflictEffect(TabId TabId, string Path) : AppEffect;
