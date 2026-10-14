using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Execution;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Rendering.Graph;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Tests.Search;

/// <summary>
/// The graph prompts wired to a real keymap engine and a real <see cref="TabSession"/> but no terminal: keys go in,
/// the selected row and the status line come out. Work posted to the "UI thread" runs at once.
/// </summary>
public sealed class PromptHarness : IDisposable
{
    private readonly KeyController keys;
    private readonly List<IPromptHandler> prompts = [];

    public PromptHarness(CommitGraph graph, bool isLoading = false, IEnumerable<GitRef>? refs = null, Queue<Action>? deferred = null)
    {
        // Search never selects a commit, so the details store never runs a process in these tests.
        var location = new RepositoryLocation(Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath(), false, false);
        Session = new TabSession(new TabId(1), GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(new GitRunner(), location), action => action(), TimeSpan.Zero));
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
            mode => keys!.SetMode(mode),
            () => Redraws++);
        prompts.Add(new Gitunia.Tui.Search.RefJumpController(handle, () => references));
        prompts.Add(new Gitunia.Tui.Search.SearchController(handle, action => (deferred is null ? action : () => deferred.Enqueue(action))()));
        Session.Changed += () => prompts.ForEach(prompt => prompt.OnGraphChanged());
        keys = new KeyController(
            new KeymapEngine(DefaultKeymap.Create()),
            (action, count) =>
            {
                Notice = string.Empty;
                foreach (var prompt in prompts)
                {
                    if (prompt.TryExecute(action, count))
                    {
                        Notice = prompt.Notice;
                        return;
                    }
                }
            })
        {
            TextKeyHandler = key => prompts.Any(prompt => prompt.HandleTextKey(key)),
        };
    }

    public TabSession Session { get; }

    public int SelectedRow { get; set; }

    public int Redraws { get; private set; }

    public string Notice { get; private set; } = string.Empty;

    public KeyMode Mode => keys.Mode;

    public string StatusLine => prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ?? string.Empty;

    public void Press(Key key) => keys.Handle(key, DefaultKeymap.GraphView);

    public void Type(string text)
    {
        foreach (var character in text)
        {
            Press(new Key(character));
        }
    }

    public void Dispose() => Session.Dispose();
}
