using Gitunia.Core.Blame;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Details;
using Gitunia.Core.Diff;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

/// <summary>
/// The same scenario as <see cref="TextFieldContractTests"/> against the fields that live in the state of a tab, a
/// screen or a view and are edited through their own reducer: the settings number, path and filter, the blame and
/// reflog search, the reflog ref picker, the sidebar and file tree filters and the diff search. A new field adds
/// itself to <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class TextFieldAppContractTests
{
    private const string Grapheme = "é";
    private const string Home = "/home/user";
    private const int PageRows = 10;

    // The registry has no number setting yet, so the number box is reached through one of its own.
    private static readonly SettingDefinition PageSize = new(
        "page-size",
        SettingCategory.Git,
        "Page size",
        SettingControl.Number,
        ["pageSize"],
        "How many rows a page has.",
        "10",
        [],
        Minimum: 5,
        Maximum: 50,
        Step: 5);

    public static TheoryData<string> Surfaces =>
    [
        "settings filter",
        "settings number",
        "settings path",
        "blame search",
        "reflog search",
        "reflog picker",
        "sidebar filter",
        "file tree filter",
        "diff search",
    ];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAField_WhenTypingAndMovingTheCaret_ThenTextAndCaretFollow(string name)
    {
        var field = Typed(name, "foo bar");

        field.Press(new KeyEvent(KeyNames.Left));

        Assert.Equal("foo bar", field.Text);
        Assert.Equal(6, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTheCaretInsideTheLastWord_WhenCtrlWIsPressed_ThenTheWordBeforeTheCaretGoes(string name)
    {
        var field = Typed(name, "foo bar");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("w", KeyModifiers.Ctrl));

        Assert.Equal("foo r", field.Text);
        Assert.Equal(4, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAGraphemeOfTwoChars_WhenBackspaceIsPressed_ThenTheWholeGraphemeGoes(string name)
    {
        var field = Typed(name, $"a{Grapheme}");

        field.Press(new KeyEvent(KeyNames.Backspace));

        Assert.Equal("a", field.Text);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTheCaretInTheMiddle_WhenTextIsPasted_ThenItGoesInAtTheCaretAndTheCaretFollows(string name)
    {
        var field = Typed(name, "ac");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("XY"));

        Assert.Equal("aXYc", field.Text);
        Assert.Equal(3, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAField_WhenCtrlUIsPressed_ThenTheTextBeforeTheCaretGoes(string name)
    {
        var field = Typed(name, "foo bar");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("u", KeyModifiers.Ctrl));

        Assert.Equal("r", field.Text);
        Assert.Equal(0, field.Caret);
    }

    private static Surface Typed(string name, string text)
    {
        var surface = Surface.Create(name);
        surface.Press(new KeyEvent(text));

        return surface;
    }

    private static AppState OpenedTab()
    {
        // The reducers never touch the disk, so the paths are plain names.
        var location = new RepositoryLocation("field-contract", "field-contract-git", "field-contract-git", false, false);

        return AppReducer.Reduce(AppState.Empty, new OpenRepositoryCommand(location)).State;
    }

    private static SettingsState Settings(params SettingDefinition[] extra)
    {
        var load = SettingsValueReader.Read(new ConfigSnapshot(true, "{}", "hash"), Home, _ => true);

        return SettingsState.Create([.. SettingsRegistry.All, .. extra], load);
    }

    private static SettingDefinition Definition(string id) => SettingsRegistry.All.Single(item => item.Id == id);

    // One field of the app behind the same four questions.
    private abstract class Surface
    {
        public abstract string Text { get; }

        public abstract int Caret { get; }

        public static Surface Create(string name) => name switch
        {
            "settings filter" => new SettingsFilterSurface(),
            "settings number" => new SettingsInputSurface(PageSize, SettingsCommandKind.Activate),
            "settings path" => new SettingsInputSurface(Definition(SettingsRegistry.ScanRootsId), SettingsCommandKind.Add),
            "blame search" => new BlameSearchSurface(),
            "reflog search" => new ReflogSurface(isPicker: false),
            "reflog picker" => new ReflogSurface(isPicker: true),
            "sidebar filter" => new SidebarFilterSurface(),
            "file tree filter" => new FileTreeFilterSurface(),
            "diff search" => new DiffSearchSurface(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown field"),
        };

        public abstract void Press(KeyEvent key);

        protected static int Clamp(string text, int caret) => PromptCaret.Clamp(text, caret);
    }

    private sealed class SettingsFilterSurface : Surface
    {
        private SettingsSession _session = SettingsSession.Empty with
        {
            Screen = SettingsReducer.Reduce(Settings(), SettingsCommandKind.FilterOpen, Home).State,
        };

        public override string Text => _session.Screen!.Filter;

        public override int Caret => Clamp(Text, _session.Screen!.FilterField.Caret);

        public override void Press(KeyEvent key) =>
            _session = SettingsScreenReducer.Reduce(_session, new SettingsKeyTyped(key)).Session;
    }

    private sealed class SettingsInputSurface(SettingDefinition definition, SettingsCommandKind open) : Surface
    {
        private SettingsSession _session = SettingsSession.Empty with
        {
            Screen = SettingsReducer.Reduce(Settings(definition).Reveal(definition), open, Home).State
                .WithInputText(new TextField()),
        };

        public override string Text => _session.Screen!.Input!.Text;

        public override int Caret => Clamp(Text, _session.Screen!.Input!.Field.Caret);

        public override void Press(KeyEvent key) =>
            _session = SettingsScreenReducer.Reduce(_session, new SettingsKeyTyped(key)).Session;
    }

    private sealed class BlameSearchSurface : Surface
    {
        private AppState _state = Viewing();

        public override string Text => _state.Shell.Blame.View!.SearchText;

        public override int Caret => Clamp(Text, _state.Shell.Blame.View!.Search.Caret);

        public override void Press(KeyEvent key) => _state = AppReducer.Reduce(_state, new BlameKeyTyped(key)).State;

        private static AppState Viewing()
        {
            var opened = AppReducer.Reduce(OpenedTab(), new BlameOpenRequested("a.txt", null)).State;
            var tab = opened.Shell.Blame.View!.TabId;
            var loaded = new BlameLoaded(tab, opened.Shell.Blame.Generation, BlameContentKind.Text, 20);
            var viewing = AppReducer.Reduce(opened, loaded).State;

            return AppReducer.Reduce(viewing, new BlameActionPressed(ActionIds.SearchOpen, null, PageRows, null)).State;
        }
    }

    private sealed class ReflogSurface(bool isPicker) : Surface
    {
        private AppState _state = Opened(isPicker);

        public override string Text => isPicker ? _state.Shell.Reflog.List!.Picker!.Text : _state.Shell.Reflog.List!.SearchText;

        public override int Caret => Clamp(Text, isPicker
            ? _state.Shell.Reflog.List!.Picker!.Field.Caret
            : _state.Shell.Reflog.List!.Search.Caret);

        public override void Press(KeyEvent key) => _state = AppReducer.Reduce(_state, new ReflogKeyTyped(key)).State;

        private static AppState Opened(bool isPicker)
        {
            var reading = AppReducer.Reduce(OpenedTab(), new ReflogOpenRequested(null));
            var read = (ReadReflogEffect)Assert.Single(reading.Effects);
            var entries = new List<ReflogEntry> { new(0, new string('a', 40), "commit", "message", DateTimeOffset.UnixEpoch) };
            var loaded = new ReflogLoaded(read.TabId, read.Generation, read.Reference, read.Title, read.Refresh, entries);
            var list = AppReducer.Reduce(reading.State, loaded).State;
            var action = isPicker ? ActionIds.ReflogOpen : ActionIds.SearchOpen;

            return AppReducer.Reduce(list, new ReflogActionPressed(action, null, PageRows)).State;
        }
    }

    private sealed class SidebarFilterSurface : Surface
    {
        private readonly TabId _tab;
        private AppState _state;

        public SidebarFilterSurface()
        {
            _state = OpenedTab();
            _tab = _state.Workspace.Tabs[0].Id;
            _state = AppReducer.Reduce(_state, new TabCommand(_tab, new FocusPaneCommand(PaneKind.Sidebar))).State;
            _state = AppReducer.Reduce(_state, new SidebarActionPressed(_tab, ActionIds.SidebarFilter, null, [PaneKind.Sidebar, PaneKind.Graph], PageRows)).State;
        }

        public override string Text => _state.Workspace.FindTab(_tab)!.Sidebar.FilterText;

        public override int Caret => Clamp(Text, _state.Workspace.FindTab(_tab)!.Sidebar.Filter.Caret);

        public override void Press(KeyEvent key) =>
            _state = AppReducer.Reduce(_state, new SidebarFilterKeyTyped(_tab, key)).State;
    }

    private sealed class FileTreeFilterSurface : Surface
    {
        private readonly TabId _tab;
        private AppState _state;

        public FileTreeFilterSurface()
        {
            _state = OpenedTab();
            _tab = _state.Workspace.ActiveTabId!.Value;
            _state = AppReducer.Reduce(_state, new TabCommand(_tab, new FocusPaneCommand(PaneKind.Detail))).State;
            _state = AppReducer.Reduce(_state, new TabCommand(_tab, new SetBottomTabCommand(BottomTab.Files))).State;
            _state = DetailReducer.Reduce(_state, new FileFilterStarted(_tab)).State;
        }

        public override string Text => _state.Workspace.FindTab(_tab)!.View.DetailTree.Filter;

        public override int Caret => Clamp(Text, _state.Workspace.FindTab(_tab)!.View.DetailTree.Field.Caret);

        public override void Press(KeyEvent key) =>
            _state = DetailReducer.Reduce(_state, new FileFilterKeyTyped(_tab, key)).State;
    }

    private sealed class DiffSearchSurface : Surface
    {
        private readonly DiffDocument _document = new DiffDocumentBuilder().Build(["alpha", "beta"]);
        private DiffSearchState _search = DiffSearch.Open().State;

        public override string Text => _search.Text;

        public override int Caret => Clamp(Text, _search.Field.Caret);

        // The same step as the diff list takes on a typed key: edit the field, then search the document.
        public override void Press(KeyEvent key)
        {
            if (_search.Field.Apply(key) is { } edit)
            {
                _search = DiffSearch.Edit(_search, edit, _document).State;
            }
        }
    }
}
