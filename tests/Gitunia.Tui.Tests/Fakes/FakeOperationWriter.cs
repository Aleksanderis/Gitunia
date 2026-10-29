using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// Records the operation calls and answers with a chosen outcome, or throws <see cref="Failure"/> like git failing.
/// </summary>
public sealed class FakeOperationWriter : IOperationWriter
{
    /// <summary>The calls in order, such as <c>continue|Rebase</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>What every call answers.</summary>
    public OperationOutcome Outcome { get; set; } = OperationOutcome.Completed;

    /// <summary>Thrown by every call when set.</summary>
    public Exception? Failure { get; set; }

    public Task<OperationResult> ContinueAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken) =>
        Answer($"continue|{operation}");

    public Task<OperationResult> AbortAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken) =>
        Answer($"abort|{operation}");

    public Task<OperationResult> SkipAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken) =>
        Answer($"skip|{operation}");

    private Task<OperationResult> Answer(string call)
    {
        Calls.Add(call);

        if (Failure is null)
        {
            return Task.FromResult(new OperationResult(Outcome, "0123456789abcdef"));
        }

        return Task.FromException<OperationResult>(Failure);
    }
}
