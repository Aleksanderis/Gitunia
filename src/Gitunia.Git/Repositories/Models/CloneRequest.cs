namespace Gitunia.Git.Repositories.Models;

/// <summary>What to clone and where.</summary>
/// <param name="Source">The URL or an absolute local path (see <see cref="CloneSource.Resolve"/>).</param>
/// <param name="TargetDirectory">The directory the repository is created in: missing or empty.</param>
public sealed record CloneRequest(string Source, string TargetDirectory);
