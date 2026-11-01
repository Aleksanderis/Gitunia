using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.App;
using Gitunia.Core.Graph;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core;
using Gitunia.Git.Commits;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Search;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Tests.Search;

/// <summary>
/// The graph prompts wired to a real keymap engine and a real <see cref="TerminalTabSession"/> but no terminal: keys go in,
/// the selected row and the status line come out. Work posted to the "UI thread" runs at once.
/// </summary>
public sealed class PromptHarness : IDisposable
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly KeyController _keys;
    private readonly List<IPromptHandler> _prompts = [];

    public PromptHarness(
        CommitGraph graph,
        bool isLoading = false,
        IEnumerable<GitRef>? refs = null,
        Queue<Action>? deferred = null,
        ICommitMessageSearcher? messages = null)
    {
        // Search never selects a commit, so the details store never runs a process in these tests.
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        Session = new TerminalTabSession(
            new TabSessionParts(
                new TabId(1),
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero)),
            GraphGlyphSet.Unicode);
        Session.Append([.. Enumerable.Range(0, graph.Count).Select(index => graph[index])]);
        if (!isLoading)
        {
            Session.Complete();
        }

        var references = refs?.ToArray() ?? [];
        var handle = new GraphViewHandle(
            () => ActiveSession ?? Session,
            () => SelectedRow,
            row => SelectedRow = row,
            _host,
            () => Redraws++);
        _prompts.Add(new QuickJumpController(new QuickJumpHost(
            () => ActiveSession ?? Session,
            () => PaneKind.Graph,
            () => new ListJumpTarget(
                PaneKind.Graph,
                text => QuickJumpLabeler.Compute(
                    text,
                    new GraphJumpRows(Session.Graph, new GraphRowOptions(80, 2, GraphGlyphSet.Unicode)),
                    new JumpWindow(FirstVisibleRow, VisibleRowCount, SelectedRow)),
                text => Session.Source.JumpText = text,
                row => SelectedRow = row),
            _host,
            () => Redraws++)));
        _prompts.Add(new RefJumpController(handle, () => references));
        Search = new SearchController(
            handle,
            action => (deferred is null ? action : () => deferred.Enqueue(action))(),
            messages,
            _ => location,
            action => action(),
            TimeSpan.Zero);
        _prompts.Add(Search);
        Session.Changed += () => _prompts.ForEach(prompt => prompt.OnGraphChanged());
        _keys = new KeyController(
            new KeymapEngine(DefaultKeymap.Create()),
            (action, count) =>
            {
                Notice = string.Empty;
                foreach (var prompt in _prompts)
                {
                    if (prompt.TryExecute(action, count))
                    {
                        Notice = prompt.Notice;
                        return;
                    }
                }
            },
            _host)
        {
            TextKeyHandler = key => _prompts.Any(prompt => prompt.HandleTextKey(key)),
        };
    }

    public TerminalTabSession Session { get; }

    public SearchController Search { get; }

    /// <summary>When set, the session the shell reports as active instead of <see cref="Session"/> (a tab switch).</summary>
    public TerminalTabSession? ActiveSession { get; set; }

    public int SelectedRow { get; set; }

    public int FirstVisibleRow { get; set; }

    public int VisibleRowCount { get; set; } = 50;

    public int Redraws { get; private set; }

    public string Notice { get; private set; } = string.Empty;

    public KeyMode Mode => _keys.Mode;

    public string StatusLine => _prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ?? string.Empty;

    public void Press(Key key) => _keys.Handle(key, DefaultKeymap.GraphView);

    public void Type(string text)
    {
        foreach (var character in text)
        {
            Press(new Key(character));
        }
    }

    public void Dispose() => Session.Dispose();
}
