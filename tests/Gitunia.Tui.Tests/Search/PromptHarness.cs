using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Search;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Tests.Search;

/// <summary>
/// The graph prompts wired to a real keymap engine and a real <see cref="TabSession"/> but no terminal: keys go in,
/// the selected row and the status line come out. Work posted to the "UI thread" runs at once.
/// </summary>
public sealed class PromptHarness : IDisposable
{
    private readonly KeyController _keys;
    private readonly List<IPromptHandler> _prompts = [];

    public PromptHarness(CommitGraph graph, bool isLoading = false, IEnumerable<GitRef>? refs = null, Queue<Action>? deferred = null)
    {
        // Search never selects a commit, so the details store never runs a process in these tests.
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        Session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
        Session.Append([.. Enumerable.Range(0, graph.Count).Select(index => graph[index])]);
        if (!isLoading)
        {
            Session.Complete();
        }

        var references = refs?.ToArray() ?? [];
        var handle = new GraphViewHandle(
            () => Session,
            () => SelectedRow,
            row => SelectedRow = row,
            mode => _keys!.SetMode(mode),
            () => Redraws++);
        _prompts.Add(new RefJumpController(handle, () => references));
        _prompts.Add(new SearchController(handle, action => (deferred is null ? action : () => deferred.Enqueue(action))()));
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
            })
        {
            TextKeyHandler = key => _prompts.Any(prompt => prompt.HandleTextKey(key)),
        };
    }

    public TabSession Session { get; }

    public int SelectedRow { get; set; }

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
