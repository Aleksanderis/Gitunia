using Gitunia.Tui.Tests.App;
using Gitunia.Tui.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.App;
using Gitunia.Core.Graph;
using Gitunia.Core.Effects;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces.Panes;
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
public sealed class PromptHarness : IDisposable, IQuickJumpHost, IActionRoute, ITextKeyHandler
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly KeyController _keys;
    private readonly List<object> _prompts = [];
    private readonly QuickJumpInput _quickJump;

    public PromptHarness(
        CommitGraph graph,
        bool isLoading = false,
        IEnumerable<GitRef>? refs = null,
        Queue<Action>? deferred = null,
        ICommitMessageSearcher? messages = null)
    {
        // Search never selects a commit, so the details store never runs a process in these tests.
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        Session = new TerminalTabSession(
            new TabSessionParts(
                _host.Workspace.ActiveTabId!.Value,
                new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)),
            GraphGlyphSet.Unicode);
        Session.Append([.. Enumerable.Range(0, graph.Count).Select(index => graph[index])]);
        if (!isLoading)
        {
            Session.Complete();
        }

        var references = refs?.ToArray() ?? [];
        var handle = new HarnessGraphView(this, location, references);
        _quickJump = new QuickJumpInput(_host, this);
        _prompts.Add(_quickJump);
        _host.EffectRaised += RunEffect;
        _prompts.Add(new RefJumpController(handle));
        Search = new SearchController(
            handle,
            new HarnessDeferral(deferred),
            InlineUi.Instance,
            messages,
            TimeSpan.Zero);
        _prompts.Add(Search);
        Session.Changed += () => _prompts.OfType<IGraphObserver>().ToList().ForEach(prompt => prompt.OnGraphChanged());
        _keys = new KeyController(new KeymapEngine(DefaultKeymap.Create()), this, _host, textKeys: this);
    }

    void IActionRoute.Route(ActionId action, int? count)
    {
        Notice = string.Empty;
        foreach (var prompt in _prompts.OfType<IActionHandler>())
        {
            if (prompt.TryExecute(action, count))
            {
                if (prompt is IActionNotice { Notice.Length: > 0 } noticed)
                {
                    Notice = noticed.Notice;
                }

                return;
            }
        }
    }

    bool ITextKeyHandler.HandleTextKey(KeyEvent key) => _prompts.OfType<ITextKeyHandler>().Any(prompt => prompt.HandleTextKey(key));

    public TerminalTabSession Session { get; }

    public SearchController Search { get; }

    public int SelectedRow { get; set; }

    public int FirstVisibleRow { get; set; }

    public int VisibleRowCount { get; set; } = 50;

    public int Redraws { get; private set; }

    public string Notice { get; private set; } = string.Empty;

    public KeyMode Mode => _keys.Mode;

    public string StatusLine => _prompts.OfType<IPromptDisplay>()
        .Select(prompt => prompt.StatusLine)
        .FirstOrDefault(line => line.Length > 0) ?? string.Empty;

    /// <summary>Opens a second tab and makes it the active one, like a tab switch.</summary>
    public void SwitchTab() =>
        _host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(
            Path.Combine(Path.GetTempPath(), "second"),
            Path.GetTempPath(),
            Path.GetTempPath(),
            false,
            false)));

    public IJumpTarget OpenTarget() => new HarnessJumpTarget(this);

    public void Redraw() => Redraws++;

    public void Press(Key key) => _keys.Handle(key, DefaultKeymap.GraphView);

    public void Type(string text)
    {
        foreach (var character in text)
        {
            Press(new Key(character));
        }
    }

    public void Dispose() => Session.Dispose();

    private void RunEffect(AppEffect effect)
    {
        if (effect is JumpToRowEffect jump)
        {
            _quickJump.JumpTo(jump.Row);
        }
        else if (effect is PostStatusEffect post)
        {
            Notice = post.Text;
        }
    }

    private sealed class HarnessGraphView(PromptHarness harness, RepositoryLocation location, GitRef[] references)
        : IGraphView
    {
        public Dispatcher Dispatcher => harness._host;

        // ReSharper disable once ReturnTypeCanBeNotNullable : test helper, members used by some tests only
        public TerminalTabSession? ActiveSession() => harness.Session;

        public int SelectedRow() => harness.SelectedRow;

        public void SelectRow(int row) => harness.SelectedRow = row;

        public void Redraw() => harness.Redraws++;

        public IEnumerable<GitRef> ActiveRefs() => references;

        // ReSharper disable once ReturnTypeCanBeNotNullable : test helper, members used by some tests only
        public RepositoryLocation? LocationOf(TabId tabId) => location;
    }

    private sealed class HarnessDeferral(Queue<Action>? deferred) : IUiDeferral
    {
        public void DeferToNextIteration(Action work)
        {
            if (deferred is null)
            {
                work();
            }
            else
            {
                deferred.Enqueue(work);
            }
        }
    }

    private sealed class HarnessJumpTarget(PromptHarness harness) : IJumpTarget
    {
        public PaneKind Pane => PaneKind.Graph;

        public string? UnavailableReason => null;

        public IReadOnlyList<JumpLabel> Labels(string text) => QuickJumpLabeler.Compute(
            text,
            new GraphJumpRows(harness.Session.Graph, new GraphRowOptions(80, 2, GraphGlyphSet.Unicode)),
            new JumpWindow(harness.FirstVisibleRow, harness.VisibleRowCount, harness.SelectedRow));

        public void ShowLabels(string? text) => harness.Session.Source.JumpText = text;

        public void JumpTo(int row) => harness.SelectedRow = row;
    }
}
