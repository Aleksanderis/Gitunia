using Gitunia.Core.Integration;
using Gitunia.Core.Status;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Integration;

[Trait("Os", "Independent")]
public sealed class OperationContinueTextsTests
{
    private const string Commit = "1234567890abcdef1234567890abcdef12345678";
    private const string Head = "abcdef1234567890abcdef1234567890abcdef12";

    [Fact]
    public void GivenEmptiedCherryPick_WhenReporting_ThenTheStatusSaysTheCommitWasDropped()
    {
        var info = new OperationInfo(RepositoryOperation.CherryPick, null, null, null, null, Commit, "feat", null);

        var report = OperationTexts.ContinueReport(info, "main", new OperationResult(OperationOutcome.Emptied, Head));

        Assert.Equal(StatusSeverity.Info, report.Severity);
        Assert.StartsWith("Nothing left to commit after resolving: dropped 1234567", report.Text);
        Assert.Contains("Cherry-pick finished", report.Text);
    }
}
