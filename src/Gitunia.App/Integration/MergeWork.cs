using Gitunia.Core.Integration;
using Gitunia.Git.Merge;

namespace Gitunia.App.Integration;

/// <summary>The git work of the merge effect.</summary>
/// <param name="writer">Runs the merge.</param>
public sealed class MergeWork(IMergeWriter writer)
{
    /// <summary>Merges; a stop on conflicts is a warning report, not a failure.</summary>
    public async Task<IntegrationCommand> MergeAsync(MergeBranchEffect effect, CancellationToken cancellationToken)
    {
        var result = await writer
            .MergeAsync(effect.Location, effect.Target.Revision, effect.Mode, cancellationToken)
            .ConfigureAwait(false);
        var report = MergeTexts.Report(result, effect.Mode, effect.Branch, effect.Target.Label);

        return new IntegrationSucceeded(effect.TabId, report.Text, report.Severity, report.Detail);
    }
}
