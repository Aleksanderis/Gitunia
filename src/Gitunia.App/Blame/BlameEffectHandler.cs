using System.Diagnostics;
using Gitunia.Core.Blame;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Text;
using Gitunia.Git.Blame;
using Gitunia.Git.Blame.Models;

namespace Gitunia.App.Blame;

/// <summary>
/// Reads the file and blames it off the UI thread: the lines first (so the view shows them at once), then the commits
/// as git finds them, filled into the <see cref="BlameStore"/> and announced in batches. It never reads or changes
/// state. One blame runs at a time; opening another or closing the view cancels it.
/// </summary>
/// <param name="reader">Reads the lines and the blame.</param>
/// <param name="store">Holds the lines and their commits for the view.</param>
/// <param name="dispatcher">Where the results go.</param>
/// <param name="tasks">Remembers the running read so the shutdown can wait for it; the default is a private set.</param>
public sealed class BlameEffectHandler(IBlameReader reader, BlameStore store, IDispatcher dispatcher, ShutdownTasks? tasks = null)
{
    /// <summary>The least time between two progress announcements, so a fast blame does not flood the dispatcher.</summary>
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(50);

    private readonly ShutdownTasks _tasks = tasks ?? new ShutdownTasks();
    private CancellationTokenSource? _running;
    private long _runningGeneration;

    /// <summary>Starts the read and returns at once; the results arrive through the dispatcher.</summary>
    /// <param name="effect">What to blame.</param>
    /// <param name="cancellationToken">Ends the read when the tab closes.</param>
    public void Start(StartBlameEffect effect, CancellationToken cancellationToken)
    {
        Cancel();
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _running = source;
        _runningGeneration = effect.Generation;
        var token = source.Token;
        _tasks.Run(() => RunAsync(effect, token));
    }

    /// <summary>Cancels the blame of the generation and frees its lines.</summary>
    /// <param name="effect">The generation to stop.</param>
    public void Stop(StopBlameEffect effect)
    {
        if (_runningGeneration <= effect.Generation)
        {
            Cancel();
        }

        store.Clear(effect.Generation);
    }

    /// <summary>Finds the next matching line and posts <see cref="BlameMatchFound"/>.</summary>
    /// <param name="effect">What to look for.</param>
    public void Find(FindBlameMatchEffect effect)
    {
        var index = -1;
        if (store.Current is { } data && data.Generation == effect.Generation)
        {
            index = BlameSearch.Find(data, effect.Query, effect.Start, effect.Forward, effect.Count);
        }

        dispatcher.Post(new BlameMatchFound(effect.Generation, effect.Query, index));
    }

    /// <summary>The text to copy for a selection: the code of the lines, or exactly the selected characters.</summary>
    /// <param name="effect">The selection.</param>
    /// <returns>The copy to make, null when the lines are gone or the selection holds no text.</returns>
    public CopyToClipboardEffect? CopyOf(CopyBlameSelectionEffect effect)
    {
        if (store.Current is not { } data || data.Generation != effect.Generation || data.Count == 0)
        {
            return null;
        }

        var first = Math.Clamp(effect.FirstLine, 0, data.Count - 1);
        var last = Math.Clamp(effect.LastLine, first, data.Count - 1);
        if (effect.Chars is { } chars)
        {
            var selected = chars.TextIn(data);
            var characters = selected.EnumerateRunes().Count();
            var description = characters == 1 ? "1 selected character" : $"{characters} selected characters";

            return selected.Length == 0 ? null : new CopyToClipboardEffect(selected, description);
        }

        var count = last - first + 1;
        var text = string.Join('\n', Enumerable.Range(first, count).Select(data.TextOf));

        return new CopyToClipboardEffect(text, count == 1 ? "1 line of code" : $"{count} lines of code");
    }

    private void Cancel()
    {
        if (_running is not { } source)
        {
            return;
        }

        _running = null;
        source.Cancel();
        source.Dispose();
    }

    private async Task RunAsync(StartBlameEffect effect, CancellationToken token)
    {
        try
        {
            var ignoreRevs = await reader.ReadIgnoreRevsFileAsync(effect.Location, token).ConfigureAwait(false);
            var content = await reader
                .ReadContentAsync(effect.Location, effect.Path, effect.Revision, token)
                .ConfigureAwait(false);
            var count = content.Lines.Count;
            token.ThrowIfCancellationRequested();
            if (content.Kind == BlameContentKind.Text)
            {
                store.Publish(new BlameData(effect.Generation, content.Lines));
            }

            dispatcher.Post(new BlameLoaded(effect.TabId, effect.Generation, content.Kind, count, ignoreRevs));
            if (content.Kind == BlameContentKind.Text)
            {
                await BlameLinesAsync(effect, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The view closed or another opened: nothing to tell.
        }
        catch (Exception exception)
        {
            dispatcher.Post(new BlameFailed(
                effect.Generation,
                $"Could not blame {effect.Path}: {FailureText.Reason(exception)}"));
        }
    }

    private async Task BlameLinesAsync(StartBlameEffect effect, CancellationToken token)
    {
        if (store.Current is not { } data || data.Generation != effect.Generation)
        {
            return;
        }

        var blamed = 0;
        var clock = Stopwatch.StartNew();
        var lastAnnounced = TimeSpan.Zero;
        await foreach (var chunk in reader
            .BlameAsync(effect.Location, effect.Path, effect.Revision, effect.Options, null, token)
            .ConfigureAwait(false))
        {
            blamed += data.Apply(chunk);
            if (clock.Elapsed - lastAnnounced >= ProgressInterval)
            {
                lastAnnounced = clock.Elapsed;
                dispatcher.Post(new BlameProgressed(effect.Generation, blamed));
            }
        }

        dispatcher.Post(new BlameFinished(effect.Generation, blamed));
    }
}
