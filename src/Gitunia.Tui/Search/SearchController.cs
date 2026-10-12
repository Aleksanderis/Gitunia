using Gitunia.Core.Keymap;
using Gitunia.Core.Search;
using Gitunia.Tui.App;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>
/// Runs the graph search of the active tab: forwards keys to the pure <see cref="GraphSearchReducer"/> and carries
/// out its effects. Scanning happens in slices on the UI thread, queued one by one, so input stays responsive even
/// on a 100k-commit log and a newer query simply makes the older slices stop.
/// </summary>
public sealed class SearchController(GraphViewHandle view, Action<Action> postToUi) : IPromptHandler
{
    private const int SliceSize = 5000;

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
            return session.Search.Notice.Length > 0 ? text + "  " + session.Search.Notice : text;
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (view.ActiveSession() is not { } session)
        {
            return false;
        }

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
        if (view.ActiveSession() is { } session && session.Search.Query is not null)
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
                case RevealMatchEffect reveal:
                    view.SelectRow(reveal.Index);
                    break;
            }
        }

        view.Redraw();
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
