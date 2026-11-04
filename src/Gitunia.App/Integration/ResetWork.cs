using System.Collections.Immutable;
using Gitunia.Core.Integration;
using Gitunia.Core.Status;
using Gitunia.Git.Repositories;
using Gitunia.Git.Reset;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;

namespace Gitunia.App.Integration;

/// <summary>The git work of the reset effects.</summary>
/// <param name="writer">Runs the reset.</param>
/// <param name="statusReader">Reads what the working tree holds, to name it in the dialog.</param>
/// <param name="operations">Tells whether a merge, rebase or similar is stopped.</param>
public sealed class ResetWork(IResetWriter writer, IStatusReader statusReader, IRepositoryOperationDetector operations)
{
    /// <summary>Reads the stopped operation and, for mixed and hard, the tracked changes the reset drops.</summary>
    public async Task<IntegrationCommand> PrepareAsync(PrepareResetEffect effect, CancellationToken cancellationToken)
    {
        var operation = operations.Detect(effect.Location);
        if (effect.Target.Mode == ResetMode.Soft)
        {
            return new ResetPrepared(effect.TabId, effect.Target, operation, []);
        }

        var snapshot = await statusReader
            .ReadStatusAsync(effect.Location, false, UntrackedFilesMode.Normal, cancellationToken)
            .ConfigureAwait(false);
        var changes = snapshot.Entries.Where(ResetTexts.IsTracked).ToImmutableArray();

        return new ResetPrepared(effect.TabId, effect.Target, operation, changes);
    }

    /// <summary>Moves the branch and names the old tip for the undo.</summary>
    public async Task<IntegrationCommand> ResetAsync(ResetBranchEffect effect, CancellationToken cancellationToken)
    {
        var target = effect.Target;
        var oldHash = await writer
            .ResetAsync(effect.Location, target.Mode, target.CommitHash, cancellationToken)
            .ConfigureAwait(false);

        return new IntegrationSucceeded(effect.TabId, ResetTexts.Done(target, oldHash),
            StatusSeverity.Info,
            ResetTexts.DoneDetail(target, oldHash));
    }
}
