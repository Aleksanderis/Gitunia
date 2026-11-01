using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;

namespace Gitunia.Core.Integration;

/// <summary>
/// Reset, merge and the operation in progress (continue, abort, skip) as pure transitions
/// <c>(IntegrationCommand, IntegrationContext) -> Effects</c>. A case here only picks the reducer of the action; the
/// results of every call end the same way: the status line, then the tab is read again.
/// </summary>
public static class IntegrationReducer
{
    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(IntegrationCommand command, IntegrationContext context) =>
        command switch
        {
            IntegrationCancelled => [],
            IntegrationFailed failed => Finished(failed.Text, StatusSeverity.Error, failed.Refresh, context),
            IntegrationSucceeded succeeded => Finished(succeeded.Text, succeeded.Severity, true, context),
            MergeRequested merge => MergeReducer.Reduce(merge, context),
            OperationAbortConfirmed or OperationRequested or OperationSkipConfirmed =>
                OperationReducer.Reduce(command, context),
            ResetConfirmed or ResetModeChosen or ResetPrepared or ResetRequested =>
                ResetReducer.Reduce(command, context),
            _ => throw new UnhandledCommandException(command),
        };

    private static ImmutableArray<AppEffect> Finished(
        string text,
        StatusSeverity severity,
        bool refresh,
        IntegrationContext context)
    {
        if (!refresh)
        {
            return [new PostStatusEffect(text, severity)];
        }

        return [new PostStatusEffect(text, severity), new RefreshTabEffect(context.TabId)];
    }
}
