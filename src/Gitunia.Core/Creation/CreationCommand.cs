using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Creation;

/// <summary>A command of the clone and init flow of the repository picker: user intent, the answers of its prompts
/// and the results of its effects.</summary>
public abstract record CreationCommand : AppCommand;

/// <summary>Alt-c or Alt-i in the picker: ask for the source or the directory.</summary>
/// <param name="Kind">What was asked for.</param>
public sealed record CreationRequested(CreationKind Kind) : CreationCommand;

/// <summary>The clone source was typed.</summary>
/// <param name="Text">The typed text.</param>
public sealed record CloneSourceAnswered(string Text) : CreationCommand;

/// <summary>Result: the folder the clone goes into by default was found.</summary>
/// <param name="Source">The resolved clone source.</param>
/// <param name="Parent">The folder.</param>
public sealed record CloneParentResolved(string Source, string Parent) : CreationCommand;

/// <summary>The clone target directory was typed.</summary>
/// <param name="Source">The resolved clone source asked before.</param>
/// <param name="Text">The typed text.</param>
public sealed record CloneTargetAnswered(string Source, string Text) : CreationCommand;

/// <summary>The directory to initialise was typed.</summary>
/// <param name="Text">The typed text.</param>
public sealed record InitDirectoryAnswered(string Text) : CreationCommand;

/// <summary>Esc or the cancel chip: stop the running clone (nothing happens when none runs).</summary>
public sealed record CloneCancelRequested : CreationCommand;

/// <summary>Progress: git reported a stage of the running clone.</summary>
/// <param name="Report">What git reported.</param>
public sealed record CloneProgressed(RemoteProgress Report) : CreationCommand;

/// <summary>Result: the clone finished.</summary>
/// <param name="Location">The new repository.</param>
public sealed record CloneSucceeded(RepositoryLocation Location) : CreationCommand;

/// <summary>Result: the clone failed.</summary>
/// <param name="Text">The reason with what was tried.</param>
public sealed record CloneFailed(string Text) : CreationCommand;

/// <summary>Result: the clone was cancelled; the creator removed what it made.</summary>
public sealed record CloneCancelled : CreationCommand;

/// <summary>Result: the init finished.</summary>
/// <param name="Location">The repository.</param>
/// <param name="WasRepository">The directory already was a repository: it is only opened.</param>
public sealed record InitSucceeded(RepositoryLocation Location, bool WasRepository) : CreationCommand;

/// <summary>Result: the init failed.</summary>
/// <param name="Text">The reason with what was tried.</param>
public sealed record InitFailed(string Text) : CreationCommand;
