using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Tests.Staging;

/// <summary>A test repository's location and the status entry of one path in it.</summary>
public sealed record LocatedEntry(RepositoryLocation Location, StatusEntry Entry);
