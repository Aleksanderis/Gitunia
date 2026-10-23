using Gitunia.Git.IssueTracker.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.IssueTracker;

/// <summary>Reads the <c>.issuetracker</c> file of a repository; the seam tests replace.</summary>
public interface IIssueTrackerReader
{
    /// <summary>
    /// The complete sections of the file in the working tree root, in file order. A missing, empty or malformed file,
    /// or a bare repository, gives an empty list; this never throws for a bad file.
    /// </summary>
    Task<IReadOnlyList<IssueTrackerRule>> ReadAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
