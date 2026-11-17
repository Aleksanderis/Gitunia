using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Editing;
using Gitunia.Core.Overlays;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

// The other surfaces of the contract: each one builds its real frame for a list of Items rows with the selection at the
// top, one down, in the middle, one before the end and at the end, at several window sizes, and the row the frame
// highlights must be the selected one. Empty lists show no highlighted row (editors and the conflict editor keep their
// cursor on a line that is not an item).
public partial class ListWindowContractTests
{
    private const string Prefix = "item";

    private static readonly int[] Selections = [0, 1, Items / 2, Items - 2, Items - 1];

    private static readonly int[] Heights = [40, 24, 16, 12];

    private const string Home = "window-contract-home";

    /// <summary>Overlays and panes whose frame highlights the selected row; the name picks the frame builder.</summary>
    public static TheoryData<string> HighlightSurfaces =>
    [
        "settings",
        "help",
        "prompt-popup",
        "picker-flat",
        "picker-grouped",
        "blame",
        "conflict-versions",
        "conflict-result",
        "message-editor",
        "message-box",
        "working-list",
    ];

    [Theory]
    [MemberData(nameof(HighlightSurfaces))]
    public void GivenAFrame_WhenTheSelectionMoves_ThenTheHighlightedRowIsTheSelectedOne(string surface)
    {
        foreach (var height in Heights)
        {
            foreach (var selected in Selections)
            {
                Assert.Equal(selected, Highlighted(surface, Items, selected, height));
            }
        }
    }

    [Theory]
    [MemberData(nameof(HighlightSurfaces))]
    public void GivenAFrameSelectionAtTheEnd_WhenTheWindowShrinks_ThenTheSelectedRowStaysShown(string surface)
    {
        foreach (var height in Heights.Order())
        {
            Assert.Equal(Items - 1, Highlighted(surface, Items, Items - 1, height));
        }
    }

    [Theory]
    [MemberData(nameof(HighlightSurfaces))]
    public void GivenAnEmptyList_WhenBuildingTheFrame_ThenNothingIsHighlightedAndNothingThrows(string surface)
    {
        var shown = Highlighted(surface, 0, 0, 24);

        Assert.Equal(KeepsCursorOnEmptyList(surface) ? 0 : null, shown);
    }

    private static bool KeepsCursorOnEmptyList(string surface) =>
        IsEditor(surface) || surface.StartsWith("conflict", StringComparison.Ordinal);

    [GeneratedRegex(@"item(\d{3})")]
    private static partial Regex ItemNumber();

    private static string Name(int index) => string.Create(CultureInfo.InvariantCulture, $"{Prefix}{index:D3}");

    // The number of the item on the one line that is highlighted; null when no line is.
    private static int? Highlighted(string surface, int count, int selected, int height)
    {
        var lines = surface switch
        {
            "settings" => SettingsLines(count, selected, height),
            "help" => HelpLines(count, selected, height),
            "prompt-popup" => PromptLines(count, selected, height),
            "picker-flat" => PickerLines(count, selected, height, "r"),
            "picker-grouped" => PickerLines(count, selected, height, string.Empty),
            "blame" => BlameLines(count, selected, height),
            "conflict-versions" => ConflictLines(count, selected, height, ConflictPane.Ours),
            "conflict-result" => ConflictLines(count, selected, height, ConflictPane.Result),
            "message-editor" => EditorLines(count, selected, height),
            "message-box" => MessageBoxLines(count, selected, height),
            _ => WorkingLines(count, selected, height),
        };
        var highlighted = lines.Where(line => IsMarked(line) || IsEditor(surface) && HasCursor(line)).ToList();
        if (highlighted.Count == 0)
        {
            return null;
        }

        var match = ItemNumber().Match(Assert.Single(highlighted).Text);

        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static bool IsMarked(StyledLine line) =>
        line.IsSelected || line.Segments.Any(segment => segment.Selection == SegmentSelection.Focused);

    private static bool HasCursor(StyledLine line) => line.Segments.Any(segment => segment.Role == SegmentRole.Cursor);

    // The cursor cell marks the line in the text editors; the query line of a picker has one too, but is not a row.
    private static bool IsEditor(string surface) => surface is "message-editor" or "message-box";

    private static IReadOnlyList<StyledLine> SettingsLines(int count, int selected, int height)
    {
        var definitions = Enumerable.Range(0, count)
            .Select(index => new SettingDefinition(
                Name(index),
                SettingCategory.Appearance,
                Name(index),
                SettingControl.Toggle,
                [Name(index)],
                string.Empty,
                string.Empty,
                []))
            .ToList();
        var state = new SettingsState(definitions, [], [], string.Empty, SettingsColumn.Settings, IsCollapsed: true)
            .MoveTo(selected);

        return SettingsFrameBuilder.Build(state, 60, height, GraphGlyphSet.Unicode)!.Lines;
    }

    // The active page is the marked entry of the section menu; the page content never names an item.
    private static IReadOnlyList<StyledLine> HelpLines(int count, int selected, int height)
    {
        var tabs = Enumerable.Range(0, count).Select(index => new HelpTab(Name(index), [])).ToList();
        var frame = HelpFrameBuilder.Build(tabs, selected, "Graph", 0, 100, height, GraphGlyphSet.Unicode);

        return [.. (frame?.Lines ?? []).Select(MarkMenuEntry)];
    }

    private static StyledLine MarkMenuEntry(StyledLine line) =>
        line.Text.Contains($"> {Prefix}", StringComparison.Ordinal) ? line with { IsSelected = true } : line;

    private static IReadOnlyList<StyledLine> PromptLines(int count, int selected, int height)
    {
        var choices = ImmutableArray.CreateRange(
            Enumerable.Range(0, count).Select(index => new PromptChoice(Name(index), Name(index))));
        var picker = PromptPicker.Of(choices, "none", string.Empty).Moved(selected);
        var prompt = PromptPopup.ForFilter("Pick", "Context", new TextField(), picker.Rows(), "ok");

        return PromptPopupFrameBuilder.Build(prompt, 100, height, GraphGlyphSet.Unicode)!.Lines;
    }

    private static IReadOnlyList<StyledLine> PickerLines(int count, int selected, int height, string query)
    {
        var rows = Enumerable.Range(0, count)
            .Select(index => new PickerRow(Path.Combine(Home, "code", Name(index)), Name(index), TagOf(index, count)))
            .ToImmutableArray();
        var picker = new PickerState(
            query,
            selected,
            rows,
            string.Empty,
            null,
            Expanded: PickerSection.Open | PickerSection.Recent);

        return PickerComposer.Compose(picker, Home, 100, height, GraphGlyphSet.Unicode);
    }

    // The first half is open, the second recent: two groups with headers and a spacer, both expanded past their caps.
    private static PickerTags TagOf(int index, int count) => index < count / 2 ? PickerTags.Open : PickerTags.Recent;

    private static IReadOnlyList<StyledLine> BlameLines(int count, int selected, int height)
    {
        var state = new BlameState(new TabId(1), "a.txt", "a.txt", LineCount: count).MoveTo(selected);
        var data = new BlameData(1, [.. Enumerable.Range(0, count).Select(Name)]);
        var frame = BlameFrameBuilder.Build(state, data, Now, 100, height, GraphGlyphSet.Unicode)!;

        var marked = frame.Lines.ToList().FindIndex(line => line.IsSelected);
        if (marked >= 0)
        {
            Assert.Equal(selected, BlameFrameBuilder.LineIndexAt(state, marked, height));
        }

        return frame.Lines;
    }

    // Common lines followed by one conflict, so line N of the version panes and of the result is item N; the cursor is
    // a row of the version panes or a line of the result.
    private static IReadOnlyList<StyledLine> ConflictLines(int count, int selected, int height, ConflictPane pane)
    {
        var common = string.Concat(Enumerable.Range(0, count).Select(index => $"{Name(index)}\n"));
        var text = $"{common}<<<<<<< ours\nours\n=======\ntheirs\n>>>>>>> theirs\n";
        var created = ConflictEditorState.Create(new TabId(1), "a.txt", ConflictParser.Parse(text).Parts!);
        var state = (pane == ConflictPane.Result ? created.FocusPane(ConflictPane.Result) : created)
            .MoveToTop()
            .MoveCursor(selected);
        var frame = ConflictEditorFrameBuilder.Build(state, 100, height, GraphGlyphSet.Unicode)!;
        var resultStart = ConflictEditorFrameBuilder.LayoutFor(state, 100, height)!.ResultStartLine;
        var marked = Enumerable.Range(0, frame.Lines.Count)
            .FirstOrDefault(
                line => (line >= resultStart) == (pane == ConflictPane.Result) && IsMarked(frame.Lines[line]),
                -1);
        Assert.True(marked >= 0, $"conflict {pane} at height {height}: no marked line");

        var target = ConflictEditorFrameBuilder.TargetAt(state, marked, 2, 100, height)!;
        Assert.Equal(selected, target.Index);

        return frame.Lines;
    }

    private static IReadOnlyList<StyledLine> EditorLines(int count, int selected, int height)
    {
        var editor = EditorOf(count, selected);

        return MessageEditorRenderer.Render(editor, 40, Math.Max(1, height / 4), true);
    }

    // The draft keeps the first shown line between frames, so the cursor goes to the top, to the end and back to the
    // selection like a user does with gg, G and a move.
    private static IReadOnlyList<StyledLine> MessageBoxLines(int count, int selected, int height)
    {
        var draft = new CommitDraft();
        draft.Editor.SetText(TextOf(count));
        var rect = new PaneRect(0, 0, 40, Math.Max(1, height / 4));
        foreach (var row in new[] { 0, Math.Max(0, count - 1), selected })
        {
            draft.Editor.SetCursor(row, 0);
            MessageBoxRenderer.EditorLines(draft, rect, 1, true, GraphGlyphSet.Unicode);
        }

        return MessageBoxRenderer.EditorLines(draft, rect, 1, true, GraphGlyphSet.Unicode);
    }

    private static IReadOnlyList<StyledLine> WorkingLines(int count, int selected, int height)
    {
        var tree = Tree([.. Enumerable.Range(0, count).Select(index => Modified($"{Name(index)}.cs"))]);
        tree = tree with { Selection = tree.SelectionAt(WorkingTreeSection.Unstaged, selected) };
        var model = new ScreenModel("repo", "main", 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);
        var working = new WorkingTreeModel(tree, PaneKind.WorkingUnstaged);

        return ScreenComposer.ComposeWorkingTree(model, working, 100, height, GraphGlyphSet.Unicode);
    }

    private static string TextOf(int count) => string.Join('\n', Enumerable.Range(0, count).Select(Name));

    private static MessageEditor EditorOf(int count, int selected)
    {
        var editor = new MessageEditor();
        editor.SetText(TextOf(count));
        editor.SetCursor(selected, 0);

        return editor;
    }
}
