using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Commits;

/// <summary>The root reducer with one tab showing the Changes view, driven the way the keys drive it.</summary>
public sealed class CommitHarness
{
    private const string HeadHash = "1111111111111111111111111111111111111111";

    public CommitHarness(params StatusEntry[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitunia-commit-tests", "alpha");
        State = AppState.Empty;
        TabId = Open(root);
        Entries = entries;
        Load();
        Send(new TabCommand(TabId, new OpenWorkingTreeCommand()));
        Send(new TabCommand(TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));
    }

    public AppState State { get; private set; }

    public TabId TabId { get; private set; }

    public StatusEntry[] Entries { get; }

    public List<AppEffect> Effects { get; } = [];

    public RepositoryState Tab => State.Workspace.FindTab(TabId)!;

    public CommitDraft Draft => Tab.Commit.Draft ?? new CommitDraft();

    public KeyMode Mode => State.Shell.Mode;

    public bool IsEditing => CommitRules.IsEditing(State);

    public string? LastStatus => Effects.OfType<PostStatusEffect>().LastOrDefault()?.Text;

    public AppUpdate Send(AppCommand command)
    {
        var update = AppReducer.Reduce(State, command);
        State = update.State;
        Effects.AddRange(update.Effects);

        return update;
    }

    // What the input layer does: the box takes the action or leaves it to the others.
    public bool Run(ActionId action)
    {
        if (!CommitRules.Takes(State, Tab, action))
        {
            return false;
        }

        Send(new CommitActionPressed(TabId, action));

        return true;
    }

    public void Type(string text)
    {
        foreach (var character in text)
        {
            Assert.True(IsEditing);
            Send(new CommitKeyTyped(TabId, new KeyEvent(character.ToString())));
        }
    }

    public void Press(KeyEvent key) => Send(new CommitKeyTyped(TabId, key));

    public void SetText(string text) => Send(new CommitDraftRestored(TabId, new SessionDraft(text, Draft.SignOff, Draft.NoVerify)));

    public void Load(string? upstream = null, int? ahead = null, HeadKind head = HeadKind.Branch)
    {
        var snapshot = Snapshot(Entries);
        var branch = snapshot.Branch with
        {
            Head = new HeadState(head, "main", HeadHash),
            UpstreamName = upstream,
            Ahead = ahead,
        };
        Send(new TabCommand(TabId, new WorkingTreeLoadedCommand(snapshot with { Branch = branch })));
        Send(new TabCommand(TabId, new HeadLoadedCommand(new HeadState(head, "main", HeadHash))));
    }

    public void SetOperation(RepositoryOperation operation)
    {
        var info = new OperationInfo(operation, null, null, null, null, null, null, null);
        Send(new TabCommand(TabId, new OperationLoadedCommand(info)));
    }

    // A running operation of the kind, as the root reducer would have marked it.
    public void Mark(RunningOperationKind kind)
    {
        var running = new RunningOperations([new RunningOperation(1, kind)], 1);
        var tabs = State.Workspace.Tabs.SetItem(0, Tab with { Running = running });
        State = State with { Workspace = State.Workspace with { Tabs = tabs } };
    }

    public void WarnQuit() => State = State with { Shell = State.Shell with { QuitWarned = true } };

    public TabId OpenOtherTab() => Open(Path.Combine(Path.GetTempPath(), "gitunia-commit-tests", "other"));

    public void Activate(TabId tabId) => Send(new ActivateTabCommand(tabId));

    public T Take<T>()
        where T : AppEffect => Effects.OfType<T>().Last();

    private TabId Open(string root)
    {
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        Send(new OpenRepositoryCommand(location));

        return State.Workspace.ActiveTabId!.Value;
    }
}
