using Gitunia.Core.Pick;
using Gitunia.Core.Text;
using Gitunia.Git.Pick;

namespace Gitunia.App.Pick;

/// <summary>
/// Runs the cherry-pick and revert effects off the UI thread and posts exactly one result command for each: the commits
/// read, what git did, a failure with git's first error line, or cancellation. It never reads or changes state.
/// Timeouts stay in the git layer.
/// </summary>
/// <param name="writer">Runs the cherry-pick or revert.</param>
/// <param name="reader">Reads the commits before anything runs.</param>
/// <param name="dispatcher">Where the result commands go.</param>
public sealed class PickEffectHandler(IPickWriter writer, IPickCommitReader reader, IDispatcher dispatcher)
{
    /// <summary>Starts the effect and returns at once; the result arrives through the dispatcher.</summary>
    public void Handle(PickEffect effect, CancellationToken cancellationToken)
    {
        EffectWork.Start(
            dispatcher,
            token => WorkAsync(effect, token),
            () => new PickCancelled(effect.TabId),
            exception => Failure(effect, FailureText.Reason(exception)),
            cancellationToken);
    }

    private Task<PickCommand> WorkAsync(PickEffect effect, CancellationToken token) => effect switch
    {
        ReadPickCommitsEffect read => ReadAsync(read, token),
        RunPickEffect run => RunPickAsync(run, token),
        _ => throw new UnhandledEffectException(effect),
    };

    private async Task<PickCommand> ReadAsync(ReadPickCommitsEffect effect, CancellationToken token)
    {
        var commits = await reader.ReadAsync(effect.Location, effect.Hashes, token).ConfigureAwait(false);

        return new PickCommitsRead(effect.TabId, effect.PickKind, [.. commits]);
    }

    private async Task<PickCommand> RunPickAsync(RunPickEffect effect, CancellationToken token)
    {
        var hashes = effect.Commits.Select(commit => commit.Hash).ToList();
        var result = await writer.PickAsync(effect.Location, effect.PickKind, hashes, effect.Mainline, token)
            .ConfigureAwait(false);

        var what = PickTexts.Describe(effect.Commits);

        return new PickFinished(effect.TabId, effect.PickKind, result, effect.Target, what);
    }

    private static PickCommand Failure(PickEffect effect, string reason)
    {
        if (effect is RunPickEffect run)
        {
            var what = PickTexts.Describe(run.Commits);

            return new PickFailed(effect.TabId, PickTexts.Failure(run.PickKind, what, run.Target, reason));
        }

        return new PickReadFailed(effect.TabId, $"Could not read the commits, nothing was done: {reason}");
    }
}
