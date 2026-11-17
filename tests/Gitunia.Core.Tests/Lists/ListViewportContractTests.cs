using System.Globalization;
using Gitunia.Core.Blame;
using Gitunia.Core.Conflicts;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Search.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Conflicts;
using Gitunia.Git.History.Models;
using Gitunia.Git.Rebase;
using Gitunia.Git.Reflog.Models;
using Xunit;

namespace Gitunia.Core.Tests.Lists;

/// <summary>
/// The list contract (ui-consistency decisions 8 and 9): every list surface stops at its ends and keeps the selection
/// in its window. A new list surface adds a row to <see cref="CursorSurfaces"/> (or a window row to
/// <see cref="WindowPolicies"/>) and gets the same scenarios.
/// </summary>
[Trait("Os", "Independent")]
public class ListViewportContractTests
{
    private const int Items = 30;

    private static readonly string Hash = new('a', 40);

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Every state type with a list cursor: its name, and how to build it with N items and move it.</summary>
    public static TheoryData<string> CursorSurfaces =>
    [
        "palette",
        "prompt-picker",
        "reflog-ref-picker",
        "ref-jump",
        "reflog",
        "file-history",
        "rebase",
        "settings",
        "blame",
        "conflict-editor",
    ];

    public static TheoryData<ScrollPolicy> WindowPolicies => [ScrollPolicy.Edge, ScrollPolicy.Centre];

    [Theory]
    [MemberData(nameof(CursorSurfaces))]
    public void GivenAnySurface_WhenMovingUpAtTheTop_ThenTheCursorStays(string surface)
    {
        Assert.Equal(0, Surface.Create(surface, Items).Move(-1).Selected);
        Assert.Equal(0, Surface.Create(surface, Items).Move(-100).Selected);
    }

    [Theory]
    [MemberData(nameof(CursorSurfaces))]
    public void GivenAnySurface_WhenMovingDownAtTheEnd_ThenTheCursorStays(string surface)
    {
        var atEnd = Surface.Create(surface, Items).Move(Items - 1);

        Assert.Equal(Items - 1, atEnd.Selected);
        Assert.Equal(Items - 1, atEnd.Move(1).Selected);
        Assert.Equal(Items - 1, atEnd.Move(500).Selected);
    }

    [Theory]
    [MemberData(nameof(CursorSurfaces))]
    public void GivenAnySurface_WhenMovingByACount_ThenTheCursorMovesThatFar(string surface)
    {
        Assert.Equal(7, Surface.Create(surface, Items).Move(7).Selected);
    }

    [Theory]
    [MemberData(nameof(CursorSurfaces))]
    public void GivenAnEmptyList_WhenMoving_ThenTheCursorStaysAtZero(string surface)
    {
        var empty = Surface.Create(surface, 0);

        Assert.Equal(0, empty.Move(1).Selected);
        Assert.Equal(0, empty.Move(-1).Selected);
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenKAtTheTop_ThenTheSelectionAndWindowStay(ScrollPolicy policy)
    {
        var moved = ListViewport.Of(0, Items, 8).Move(ActionIds.ListUp, null, policy);

        Assert.Equal(new ListViewport(0, 0, Items, 8), moved);
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenG_ThenTheLastItemShowsAtTheBottom(ScrollPolicy policy)
    {
        var moved = ListViewport.Of(3, Items, 8).Move(ActionIds.ListBottom, null, policy);

        Assert.NotNull(moved);
        Assert.Equal(Items - 1, moved.Value.Selected);
        Assert.Equal(Items - 8, moved.Value.Top);
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenCtrlDAtTheEnd_ThenNothingMoves(ScrollPolicy policy)
    {
        var atEnd = ListViewport.Of(Items - 1, Items - 8, Items, 8);
        var moved = atEnd.Move(ActionIds.ListHalfPageDown, null, policy);

        Assert.Equal(atEnd, moved);
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenACountIsGiven_ThenTheSelectionMovesThatManyRows(ScrollPolicy policy)
    {
        var moved = ListViewport.Of(0, Items, 8).Move(ActionIds.ListDown, 12, policy);

        Assert.NotNull(moved);
        Assert.Equal(12, moved.Value.Selected);
        Assert.True(moved.Value.IsVisible(12));
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenTheWindowShrinks_ThenTheSelectionStaysVisible(ScrollPolicy policy)
    {
        var large = ListViewport.Of(20, 10, Items, 12).Follow(policy);
        var small = (large with { Rows = 3 }).Follow(policy);

        Assert.True(large.IsVisible(20));
        Assert.True(small.IsVisible(20));
        Assert.Equal(3, small.Window.RowCount);
    }

    [Theory]
    [MemberData(nameof(WindowPolicies))]
    public void GivenAnyPolicy_WhenTheListIsEmpty_ThenTheWindowIsOneRowAtZero(ScrollPolicy policy)
    {
        var empty = ListViewport.Of(5, 0, 8).Follow(policy);

        Assert.Equal(new ListWindow(1, 0, 0), empty.Window);
        Assert.Null(ListViewport.Of(0, 0, 8).Move(ActionIds.RepositoryRefresh, null, policy));
    }

    [Fact]
    public void GivenTheEdgePolicy_WhenMovingInsideTheWindow_ThenTheWindowDoesNotMove()
    {
        var viewport = ListViewport.Of(5, 3, Items, 8).Follow(ScrollPolicy.Edge);

        Assert.Equal(3, viewport.MoveBy(2, ScrollPolicy.Edge).Top);
        Assert.Equal(6, viewport.MoveBy(8, ScrollPolicy.Edge).Top);
    }

    [Fact]
    public void GivenTheCentrePolicy_WhenMovingInTheMiddle_ThenTheSelectionStaysCentred()
    {
        var viewport = ListViewport.Of(15, Items, 8).Follow(ScrollPolicy.Centre);

        Assert.Equal(11, viewport.Top);
    }

    [Fact]
    public void GivenAnEditor_WhenTheCursorIsAboveTheWindow_ThenTheTopFollowsItAndNeverGoesNegative()
    {
        Assert.Equal(0, ListViewport.EdgeTopFor(0, 0, 8));
        Assert.Equal(2, ListViewport.EdgeTopFor(2, 6, 8));
        Assert.Equal(0, ListViewport.EdgeTopFor(-5, -3, 8));
    }

    [Fact]
    public void GivenAnEditor_WhenTheCursorIsBelowTheWindow_ThenTheCursorShowsAtTheBottom()
    {
        Assert.Equal(22, ListViewport.EdgeTopFor(29, 0, 8));
        Assert.Equal(3, ListViewport.EdgeTopFor(10, 3, 8));
    }

    [Fact]
    public void GivenAnEditorWindowWithoutRows_WhenScrolling_ThenTheTopStaysWhereItWas()
    {
        Assert.Equal(4, ListViewport.EdgeTopFor(20, 4, 0));
        Assert.Equal(0, ListViewport.EdgeTopFor(0, 0, 0));
    }

    private sealed record Surface(Func<int, Surface> Step, int Selected)
    {
        public Surface Move(int delta) => Step(delta);

        public static Surface Create(string name, int count) => name switch
        {
            "palette" => Palette(count),
            "prompt-picker" => Picker(count),
            "reflog-ref-picker" => RefPicker(count),
            "ref-jump" => RefJump(count),
            "reflog" => Reflog(count),
            "file-history" => FileHistory(count),
            "rebase" => Rebase(count),
            "settings" => Settings(count),
            "blame" => Blame(count),
            "conflict-editor" => ConflictEditor(count),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        private static Surface Of<TState>(
            TState state,
            Func<TState, Func<int, TState>> move,
            Func<TState, int> selected) =>
            new(delta => Of(move(state)(delta), move, selected), selected(state));

        private static PaletteEntry Entry(int index) => new(ActionIds.RepositoryRefresh, Name(index), string.Empty);

        private static PromptChoice Choice(int index) => new(Name(index), Name(index));

        private static string Name(int index) => string.Create(CultureInfo.InvariantCulture, $"item{index}");

        private static RefJumpCandidate Candidate(int index)
        {
            Assert.True(ObjectId.TryParse(index.ToString("x40", CultureInfo.InvariantCulture), out var id));

            return new RefJumpCandidate(Name(index), id, null);
        }

        private static Surface Palette(int count) => Of(
            new PaletteState([.. Enumerable.Range(0, count).Select(Entry)]),
            state => state.Move,
            state => state.SelectedIndex);

        private static Surface Picker(int count) => Of(
            PromptPicker.Of([.. Enumerable.Range(0, count).Select(Choice)], "none", string.Empty),
            state => state.Moved,
            state => state.Index);

        private static Surface RefPicker(int count) => Of(
            new ReflogRefPicker([.. Enumerable.Range(0, count).Select(Name)]),
            state => state.Move,
            state => state.Index);

        private static Surface Reflog(int count) => Of(
            new ReflogState(
                new TabId(1),
                "HEAD",
                "HEAD",
                [.. Enumerable.Range(0, count).Select(index => new ReflogEntry(index, Hash, "commit", Name(index), Now))]),
            state => delta => state.MoveTo(state.SelectedIndex + delta),
            state => state.SelectedIndex);

        private static Surface FileHistory(int count) => Of(
            new FileHistoryState(
                new TabId(1),
                "a.txt",
                "HEAD",
                [
                    .. Enumerable.Range(0, count)
                        .Select(index => new FileHistoryEntry(
                            Hash,
                            "Ann",
                            "2026-11-02",
                            Name(index),
                            "a.txt",
                            null,
                            FileChangeKind.Modified)),
                ]),
            state => delta => state.MoveTo(state.SelectedIndex + delta),
            state => state.SelectedIndex);

        private static Surface Rebase(int count) => Of(
            RebaseEditorState.Create(
                new TabId(1),
                "topic",
                "main",
                RebaseBase.OntoCommit("x"),
                "tip",
                [.. Enumerable.Range(0, count).Select(index => new RebaseTodoCommit(Hash, Name(index)))]),
            state => delta => state.MoveTo(state.SelectedIndex + delta),
            state => state.SelectedIndex);

        // The collapsed layout lists every setting in one column, so the cursor runs over all of them.
        private static Surface Settings(int count) => Of(
            new SettingsState(
                [.. Enumerable.Range(0, count).Select(Setting)],
                [],
                [],
                string.Empty,
                SettingsColumn.Settings,
                IsCollapsed: true),
            state => delta => state.MoveTo(state.Cursor + delta),
            state => state.Cursor);

        private static SettingDefinition Setting(int index) => new(
            Name(index),
            SettingCategory.Appearance,
            Name(index),
            SettingControl.Toggle,
            [Name(index)],
            string.Empty,
            string.Empty,
            []);

        private static Surface Blame(int count) => Of(
            new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: count),
            state => delta => state.MoveTo(state.Cursor + delta),
            state => state.Cursor);

        // One conflict whose ours side has N lines: N aligned rows in the version panes.
        private static Surface ConflictEditor(int count)
        {
            var lines = string.Concat(Enumerable.Range(0, count).Select(index => $"{Name(index)}\n"));
            var text = count == 0 ? string.Empty : $"<<<<<<< ours\n{lines}=======\ntheirs\n>>>>>>> theirs\n";
            var state = ConflictEditorState.Create(new TabId(1), "a.txt", ConflictParser.Parse(text).Parts ?? []);

            return Of(state, current => current.MoveCursor, current => current.Row);
        }

        private static Surface RefJump(int count) => Of(
            new RefJumpState(string.Empty, [.. Enumerable.Range(0, count).Select(Candidate)], 0),
            state => state.Move,
            state => state.SelectedIndex);
    }
}
