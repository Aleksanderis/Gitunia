using Gitunia.Core.Integration;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations;

namespace Gitunia.App.Integration;

/// <summary>The git work of the continue, abort and skip effects.</summary>
/// <param name="writer">Runs the git calls.</param>
public sealed class OperationWork(IOperationWriter writer)
{
    /// <summary>Continues; a rebase may stop again at the next commit.</summary>
    public async Task<IntegrationCommand> ContinueAsync(
        ContinueOperationEffect effect,
        CancellationToken cancellationToken)
    {
        var result = await writer.ContinueAsync(effect.Location, effect.Info.Kind, cancellationToken)
            .ConfigureAwait(false);

        return Report(effect.TabId, OperationTexts.ContinueReport(effect.Info, effect.Branch, result));
    }

    /// <summary>Aborts and names where the branch is.</summary>
    public async Task<IntegrationCommand> AbortAsync(AbortOperationEffect effect, CancellationToken cancellationToken)
    {
        var result = await writer.AbortAsync(effect.Location, effect.Info.Kind, cancellationToken)
            .ConfigureAwait(false);

        return Report(effect.TabId, OperationTexts.AbortReport(effect.Info, effect.Branch, result));
    }

    /// <summary>Skips the commit the operation stopped on.</summary>
    public async Task<IntegrationCommand> SkipAsync(SkipOperationEffect effect, CancellationToken cancellationToken)
    {
        var result = await writer.SkipAsync(effect.Location, effect.Info.Kind, cancellationToken)
            .ConfigureAwait(false);

        return Report(effect.TabId, OperationTexts.SkipReport(effect.Info, result));
    }

    private static IntegrationSucceeded Report(TabId tabId, IntegrationReport report) =>
        new(tabId, report.Text, report.Severity);
}
