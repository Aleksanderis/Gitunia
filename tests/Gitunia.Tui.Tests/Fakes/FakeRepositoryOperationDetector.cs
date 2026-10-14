using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Reports a chosen operation, so refresher tests need no state files.</summary>
public sealed class FakeRepositoryOperationDetector : IRepositoryOperationDetector
{
    /// <summary>What every repository reports.</summary>
    public RepositoryOperation Operation { get; set; } = RepositoryOperation.None;

    public RepositoryOperation Detect(RepositoryLocation location) => Operation;
}
