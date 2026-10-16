using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Reports a fixed operation in progress.</summary>
public sealed class FakeOperationDetector : IRepositoryOperationDetector
{
    public RepositoryOperation Operation { get; set; } = RepositoryOperation.None;

    public RepositoryOperation Detect(RepositoryLocation location) => Operation;
}
