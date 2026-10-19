namespace Gitunia.Git.Repositories.Models;

/// <summary>The outcome of initialising a directory.</summary>
/// <param name="Location">The repository.</param>
/// <param name="WasRepository">The directory already was a repository root: nothing was initialised again.</param>
public sealed record InitResult(RepositoryLocation Location, bool WasRepository);
