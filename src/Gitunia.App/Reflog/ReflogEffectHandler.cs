using Gitunia.Core.Reflog;
using Gitunia.Core.Text;
using Gitunia.Git.Reflog;

namespace Gitunia.App.Reflog;

/// <summary>
/// Reads the reflog of a ref off the UI thread and posts exactly one result command: the entries, the failure text or
/// the cancellation. It never reads or changes state. The timeout stays in the git layer.
/// </summary>
/// <param name="reader">Reads the reflog.</param>
/// <param name="dispatcher">Where the result command goes.</param>
public sealed class ReflogEffectHandler(IReflogReader reader, IDispatcher dispatcher)
{
    /// <summary>Starts the read and returns at once; the result arrives through the dispatcher.</summary>
    /// <param name="effect">What to read.</param>
    /// <param name="cancellationToken">Ends the read when the tab closes.</param>
    public void Handle(ReadReflogEffect effect, CancellationToken cancellationToken) =>
        EffectWork.Start(
            dispatcher,
            token => ReadAsync(effect, token),
            () => new ReflogReadCancelled(),
            exception => new ReflogReadFailed(
                $"Could not read the reflog of {effect.Title}: {FailureText.Reason(exception)}"),
            cancellationToken);

    private async Task<ReflogCommand> ReadAsync(ReadReflogEffect effect, CancellationToken token)
    {
        var read = await reader
            .ReadAsync(effect.Location, effect.Reference, ReflogReducer.EntryLimit, token)
            .ConfigureAwait(false);

        return new ReflogLoaded(
            effect.TabId,
            effect.Generation,
            effect.Reference,
            effect.Title,
            effect.Refresh,
            read.Items);
    }
}
