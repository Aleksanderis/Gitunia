using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>
/// Runs the graph search of the active tab: forwards keys to the pure <see cref="GraphSearchReducer"/> and carries
/// out its effects. Scanning happens in slices on the UI thread, queued one by one, so input stays responsive even
/// on a 100k-commit log and a newer query simply makes the older slices stop.
/// </summary>
public sealed class SearchController(
    GraphViewHandle view,
    Action<Action> postToUi,
    ICommitMessageSearcher? messages = null,
    Func<TabId, RepositoryLocation?>? locationOf = null,
    Action<Action>? invokeOnUi = null,
    TimeSpan? bodyDebounce = null) : IPromptHandler
{
    private const int SliceSize = 5000;

    private const string FailureText = "git returned an error";

    // A body query starts the search only once typing paused, so a fast typist starts no process per key.
    private static readonly TimeSpan DefaultBodyDebounce = TimeSpan.FromMilliseconds(250);

    // Touched by the UI thread and by the end of a run on a pool thread, so every access takes the lock.
    private readonly Dictionary<TabSession, CancellationTokenSource> _bodySearches = [];

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (view.ActiveSession() is not { } session || !session.Search.IsOpen)
            {
                return string.Empty;
            }

            var text = GraphSearchStatusText.Describe(session.Search, view.SelectedRow(), session.Graph.Count);
            return session.Search.Notice.Length > 0 ? $"{text}  {session.Search.Notice}" : text;
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (view.ActiveSession() is not { } session)
        {
            return false;
        }

        // A tab that was in the background while its log kept loading catches up here.
        CatchUp(session);

        if (action == ActionIds.SearchOpen)
        {
            Dispatch(session, new OpenSearchCommand());
            view.SetMode(KeyMode.Insert);
        }
        else if (action == ActionIds.SearchNext || action == ActionIds.SearchPrevious)
        {
            Dispatch(session, new StepMatchCommand(action == ActionIds.SearchNext, count ?? 1, view.SelectedRow()));
            Notice = session.Search.IsOpen ? string.Empty : session.Search.Notice;
        }
        else if (action == ActionIds.InputAccept && session.Search.Phase == SearchPhase.Editing)
        {
            Dispatch(session, new AcceptSearchCommand(view.SelectedRow()));
            view.SetMode(KeyMode.Normal);
        }
        else if (action == ActionIds.Cancel && session.Search.IsOpen)
        {
            Dispatch(session, new CancelSearchCommand());
            view.SetMode(KeyMode.Normal);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (view.ActiveSession() is not { } session || session.Search.Phase != SearchPhase.Editing)
        {
            return false;
        }

        if (PromptTextEditor.Apply(session.Search.Text, key) is { } text && text != session.Search.Text)
        {
            Dispatch(session, new EditSearchTextCommand(text));
        }

        // Insert mode swallows every key, so a stray one never moves the list or fires a binding.
        return true;
    }

    public void OnGraphChanged()
    {
        if (view.ActiveSession() is { } session)
        {
            CatchUp(session);
        }
    }

    private void CatchUp(TabSession session)
    {
        if (session.Search.Query is null)
        {
            session.SearchGraphVersion = session.GraphVersion;
        }
        else if (session.SearchGraphVersion != session.GraphVersion)
        {
            session.SearchGraphVersion = session.GraphVersion;
            Dispatch(session, new GraphReplacedCommand(session.Graph.Count));
        }
        else
        {
            Dispatch(session, new GraphGrewCommand(session.Graph.Count, !session.IsLoading));
        }
    }

    private void Dispatch(TabSession session, GraphSearchCommand command)
    {
        var update = GraphSearchReducer.Reduce(session.Search, command);
        session.Search = update.State;
        session.Source.Highlight = update.State.IsOpen ? update.State.Query : null;
        foreach (var effect in update.Effects)
        {
            switch (effect)
            {
                case ScanSearchEffect scan:
                    postToUi(() => ScanSlice(session, scan.Generation));
                    break;
                case SearchBodiesEffect bodies:
                    StartBodySearch(session, bodies);
                    break;
                case RevealMatchEffect reveal:
                    view.SelectRow(reveal.Index);
                    break;
            }
        }

        if (update.State.Query is not { IsAwaitingBodyHits: true })
        {
            CancelBodySearch(session);
        }

        view.Redraw();
    }

    // Cancels the earlier question of this tab (a newer query, or a refresh) and asks again off the UI thread.
    private void StartBodySearch(TabSession session, SearchBodiesEffect effect)
    {
        CancelBodySearch(session);
        var location = locationOf?.Invoke(session.TabId);
        if (messages is null || location is null)
        {
            postToUi(() => Dispatch(session, new BodyHitsCommand(effect.Generation, null, FailureText)));
            return;
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation.Token);
        lock (_bodySearches)
        {
            _bodySearches[session] = source;
        }

        _ = RunBodySearchAsync(session, location, effect, source);
    }

    private void CancelBodySearch(TabSession session)
    {
        CancellationTokenSource? running;
        lock (_bodySearches)
        {
            _bodySearches.Remove(session, out running);
        }

        running?.Cancel();
        running?.Dispose();
    }

    // The run of a closed tab is cancelled by its session; its entry must not outlive it.
    private void ForgetBodySearch(TabSession session, CancellationTokenSource source)
    {
        lock (_bodySearches)
        {
            if (_bodySearches.TryGetValue(session, out var current) && current == source)
            {
                _bodySearches.Remove(session);
            }
        }

        source.Dispose();
    }

    /// <summary>Number of tabs that still hold a running body search entry.</summary>
    public int BodySearchCount
    {
        get
        {
            lock (_bodySearches)
            {
                return _bodySearches.Count;
            }
        }
    }

    private async Task RunBodySearchAsync(
        TabSession session,
        RepositoryLocation location,
        SearchBodiesEffect effect,
        CancellationTokenSource source)
    {
        var token = source.Token;
        BodyHitsCommand command;
        try
        {
            await Task.Delay(bodyDebounce ?? DefaultBodyDebounce, token).ConfigureAwait(false);
            var hits = await messages!.SearchAsync(location, effect.Text, token).ConfigureAwait(false);
            command = new BodyHitsCommand(effect.Generation, hits, string.Empty);
        }
        catch (OperationCanceledException)
        {
            ForgetBodySearch(session, source);
            return;
        }
        catch (Exception exception) when (exception is GitException or IOException)
        {
            command = new BodyHitsCommand(effect.Generation, null, FailureText);
        }

        ForgetBodySearch(session, source);

        (invokeOnUi ?? postToUi)(() =>
        {
            if (!token.IsCancellationRequested)
            {
                Dispatch(session, command);
            }
        });
    }

    private void ScanSlice(TabSession session, int generation)
    {
        var search = session.Search;
        if (search.Generation != generation || search.Query is null || session.Cancellation.IsCancellationRequested)
        {
            return;
        }

        var result = GraphSearchScanner.Scan(search.Query, session.Graph, search.Scanned, SliceSize);
        Dispatch(session, new ScanProgressCommand(generation, result.Matches, result.NextIndex, session.Graph.Count, !session.IsLoading));
    }
}
