using Gitunia.Core.Config.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsReducerTests
{
    private const string Home = "/home/user";
    private const string PageSizeId = "page-size";
    private static readonly SettingsEnvironment Environment = new(
        Home,
        path => !path.Contains("missing", StringComparison.Ordinal));

    private static readonly SettingDefinition PageSize = new(
        PageSizeId,
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

    [Fact]
    public void GivenTheRegistry_WhenListingCategories_ThenOnlyThoseWithSettingsAppearInDisplayOrder()
    {
        var state = Open();

        Assert.Equal(
            [
                SettingCategory.Appearance,
                SettingCategory.Keys,
                SettingCategory.Repositories,
                SettingCategory.Tabs,
                SettingCategory.ConfigFile,
            ],
            state.Categories);
    }

    [Fact]
    public void GivenTheCategoriesColumn_WhenActivating_ThenTheCursorEntersTheSettingsOfThatCategory()
    {
        var state = Open().MoveTo(3);

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.Equal(SettingsColumn.Settings, outcome.State.Column);
        Assert.Equal(SettingsRegistry.ShowBranchId, outcome.State.Selected?.Definition.Id);
        Assert.Null(outcome.Commit);
    }

    [Theory]
    [InlineData(SettingsCommandKind.Activate)]
    [InlineData(SettingsCommandKind.Toggle)]
    public void GivenAToggleOffInTheFile_WhenFlippingIt_ThenTheCommitHoldsTrue(SettingsCommandKind command)
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ShowBranchId));

        var outcome = Reduce(state, command);

        Assert.True(outcome.Commit?.Value.Flag);
        Assert.Equal(SettingsRegistry.ShowBranchId, outcome.Commit?.Definition.Id);
        Assert.Equal("true", outcome.Commit?.Value.ToJson(SettingControl.Toggle));
    }

    [Fact]
    public void GivenAToggleOnInTheFile_WhenFlippingIt_ThenTheCommitHoldsFalse()
    {
        var state = Open("{ \"tabs\": { \"showBranch\": true } }").Reveal(Definition(SettingsRegistry.ShowBranchId));

        var outcome = Reduce(state, SettingsCommandKind.Toggle);

        Assert.False(outcome.Commit?.Value.Flag);
    }

    [Fact]
    public void GivenARowThatIsNotAToggle_WhenPressingToggle_ThenNothingHappens()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.FrameStyleId));

        var outcome = Reduce(state, SettingsCommandKind.Toggle);

        Assert.Null(outcome.Commit);
        Assert.Equal(state, outcome.State);
    }

    [Fact]
    public void GivenAChoice_WhenActivating_ThenItsOptionsAreListedWithTheCursorOnTheCurrentOne()
    {
        var state = Open("{ \"frameStyle\": \"square\" }").Reveal(Definition(SettingsRegistry.FrameStyleId));

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.Null(outcome.Commit);
        Assert.Equal(
            [SettingRowKind.Setting, SettingRowKind.Setting, SettingRowKind.Option, SettingRowKind.Option],
            outcome.State.Rows.Select(row => row.Kind));
        Assert.Equal(1, outcome.State.Selected?.Index);
    }

    [Fact]
    public void GivenAnOpenChoice_WhenKeepingAnotherOption_ThenTheCommitHoldsItAndTheOptionsClose()
    {
        var opened = OpenChoice();

        var outcome = Reduce(opened.MoveTo(3), SettingsCommandKind.Activate);

        Assert.Equal("square", outcome.Commit?.Value.Choice);
        Assert.Null(outcome.State.Choice);
        Assert.Equal(SettingRowKind.Setting, outcome.State.Selected?.Kind);
    }

    [Fact]
    public void GivenTheThemeOptions_WhenMovingTheCursor_ThenThePreviewFollowsItAndEndsWhenTheListCloses()
    {
        var opened = Reduce(Open().Reveal(Definition(SettingsRegistry.ThemeId)), SettingsCommandKind.Activate).State;

        Assert.Equal(ThemeChoice.Auto, opened.PreviewTheme);
        Assert.Equal(ThemeChoice.Dark, opened.MoveTo(2).PreviewTheme);
        Assert.Equal(ThemeChoice.Light, opened.MoveTo(3).PreviewTheme);
        Assert.Null(Reduce(opened.MoveTo(3), SettingsCommandKind.Back).State.PreviewTheme);
    }

    [Fact]
    public void GivenAChoiceThatIsNotTheTheme_WhenTheListIsOpen_ThenThereIsNoThemePreview()
    {
        Assert.Null(OpenChoice().PreviewTheme);
        Assert.Null(Open().Reveal(Definition(SettingsRegistry.ThemeId)).PreviewTheme);
    }

    [Fact]
    public void GivenTheThemeOptions_WhenKeepingLight_ThenTheCommitWritesTheThemeKey()
    {
        var closed = Open("{ \"theme\": \"dark\" }").Reveal(Definition(SettingsRegistry.ThemeId));
        var opened = Reduce(closed, SettingsCommandKind.Activate).State;
        Assert.Equal(ThemeChoice.Dark, opened.PreviewTheme);

        var outcome = Reduce(opened.MoveTo(3), SettingsCommandKind.Activate);

        Assert.Equal(["theme"], outcome.Commit?.Definition.ConfigPath);
        Assert.Equal("\"light\"", outcome.Commit?.Value.ToJson(SettingControl.Choice));
        Assert.Null(outcome.State.PreviewTheme);
    }

    [Fact]
    public void GivenAnOpenChoice_WhenKeepingTheCurrentOption_ThenNothingIsWritten()
    {
        var opened = OpenChoice();

        var outcome = Reduce(opened, SettingsCommandKind.Activate);

        Assert.Null(outcome.Commit);
        Assert.Null(outcome.State.Choice);
    }

    [Fact]
    public void GivenAnOpenChoice_WhenGoingBack_ThenNothingIsWrittenAndTheCursorReturnsToTheSetting()
    {
        var opened = OpenChoice();

        var outcome = Reduce(opened.MoveTo(3), SettingsCommandKind.Back);

        Assert.Null(outcome.Commit);
        Assert.False(outcome.Closes);
        Assert.Null(outcome.State.Choice);
        Assert.Equal(SettingRowKind.Setting, outcome.State.Selected?.Kind);
    }

    [Fact]
    public void GivenAnOpenChoice_WhenMovingFarPastTheOptions_ThenTheCursorStaysAmongThem()
    {
        var opened = OpenChoice();

        var down = opened.MoveTo(99);
        var up = opened.MoveTo(-5);

        Assert.Equal(3, down.Cursor);
        Assert.Equal(2, up.Cursor);
    }

    [Fact]
    public void GivenANumber_WhenTypingAValueInRange_ThenItIsCommitted()
    {
        var state = WithPageSize();
        var typing = Reduce(state, SettingsCommandKind.Activate).State;
        Assert.Equal("10", typing.Input?.Text);

        var outcome = Reduce(typing.WithInputText("25"), SettingsCommandKind.InputAccept);

        Assert.Equal(25, outcome.Commit?.Value.Number);
        Assert.Equal("25", outcome.Commit?.Value.ToJson(SettingControl.Number));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("51")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1.5")]
    public void GivenANumber_WhenTypingAValueOutOfRange_ThenItIsRefusedWithTheRangeAndTheBoxStaysOpen(string text)
    {
        var typing = Reduce(WithPageSize(), SettingsCommandKind.Activate).State.WithInputText(text);

        var outcome = Reduce(typing, SettingsCommandKind.InputAccept);

        Assert.Null(outcome.Commit);
        Assert.Contains("from 5 to 50", outcome.Warning, StringComparison.Ordinal);
        Assert.NotNull(outcome.State.Input);
    }

    [Fact]
    public void GivenANumber_WhenStepping_ThenItMovesByTheStepAndStopsAtTheEnds()
    {
        var state = WithPageSize();

        var up = Reduce(state, SettingsCommandKind.Increase);
        var down = Reduce(state, SettingsCommandKind.Decrease);
        var atMinimum = Reduce(state.WithNumber(PageSizeId, 5), SettingsCommandKind.Decrease);
        var atMaximum = Reduce(state.WithNumber(PageSizeId, 50), SettingsCommandKind.Increase);

        Assert.Equal(15, up.Commit?.Value.Number);
        Assert.Equal(5, down.Commit?.Value.Number);
        Assert.Null(atMinimum.Commit);
        Assert.Contains("from 5 to 50", atMinimum.Warning, StringComparison.Ordinal);
        Assert.Null(atMaximum.Commit);
    }

    [Fact]
    public void GivenAPathList_WhenAddingAnExistingFolder_ThenItIsAppendedAsTypedAndSelectedAfterTheWrite()
    {
        var state = Open("{ \"scanRoots\": [\"/srv/git\"] }").Reveal(Definition(SettingsRegistry.ScanRootsId));

        var typing = Reduce(state, SettingsCommandKind.Add).State;
        Assert.Equal(SettingRowKind.NewItem, typing.Selected?.Kind);
        var outcome = Reduce(typing.WithInputText("~/Projects"), SettingsCommandKind.InputAccept);

        Assert.Equal(["/srv/git", "~/Projects"], outcome.Commit?.Value.Paths);
        Assert.Equal(1, outcome.Commit?.SelectItem);
        Assert.Equal("[\"/srv/git\", \"~/Projects\"]", outcome.Commit?.Value.ToJson(SettingControl.PathList));
    }

    [Theory]
    [InlineData("", "Type a folder path")]
    [InlineData("relative/dir", "not an absolute path")]
    [InlineData("/srv/missing", "not an existing folder")]
    [InlineData("/srv/git/", "already in the list")]
    [InlineData("~/code", "already in the list")]
    public void GivenAPathList_WhenAddingAFolderThatCannotBeUsed_ThenItIsRefusedWithTheReasonAndNothingIsWritten(
        string typed,
        string reason)
    {
        var state = Open("{ \"scanRoots\": [\"/srv/git\", \"~/code\"] }")
            .Reveal(Definition(SettingsRegistry.ScanRootsId));
        var typing = Reduce(state, SettingsCommandKind.Add).State.WithInputText(typed);

        var outcome = Reduce(typing, SettingsCommandKind.InputAccept);

        Assert.Null(outcome.Commit);
        Assert.Contains(reason, outcome.Warning, StringComparison.Ordinal);
        Assert.NotNull(outcome.State.Input);
    }

    [Fact]
    public void GivenAFolderOfTheList_WhenEditingItToTheSamePath_ThenItIsNotADuplicateOfItself()
    {
        var state = Open("{ \"scanRoots\": [\"/srv/git\", \"/srv/other\"] }").Reveal(
            Definition(SettingsRegistry.ScanRootsId),
            SettingRowKind.Item);

        var typing = Reduce(state, SettingsCommandKind.Activate).State;
        Assert.Equal("/srv/git", typing.Input?.Text);
        var outcome = Reduce(typing.WithInputText("/srv/new"), SettingsCommandKind.InputAccept);

        Assert.Equal(["/srv/new", "/srv/other"], outcome.Commit?.Value.Paths);
        Assert.Equal(0, outcome.Commit?.SelectItem);
    }

    [Fact]
    public void GivenAFolder_WhenRemovingIt_ThenTheQuestionNamesItAndOnlyYesWritesTheListWithoutIt()
    {
        var state = Open("{ \"scanRoots\": [\"/a\", \"/b\", \"/c\"] }").Reveal(
            Definition(SettingsRegistry.ScanRootsId),
            SettingRowKind.Item,
            1);

        var asked = Reduce(state, SettingsCommandKind.Remove);
        var yes = Reduce(asked.State, SettingsCommandKind.ConfirmYes);
        var no = Reduce(asked.State, SettingsCommandKind.ConfirmNo);
        var back = Reduce(asked.State, SettingsCommandKind.Back);

        Assert.Equal("/b", asked.State.Removal?.Path);
        Assert.Null(asked.Commit);
        Assert.Equal(["/a", "/c"], yes.Commit?.Value.Paths);
        Assert.Equal(1, yes.Commit?.SelectItem);
        Assert.Null(yes.State.Removal);
        Assert.Null(no.Commit);
        Assert.Null(no.State.Removal);
        Assert.Null(back.Commit);
        Assert.False(back.Closes);
        Assert.Null(back.State.Removal);
    }

    [Fact]
    public void GivenTheOnlyFolder_WhenRemovingIt_ThenTheListBecomesEmptyAndNoItemIsSelected()
    {
        var state = Open("{ \"scanRoots\": [\"/a\"] }")
            .Reveal(Definition(SettingsRegistry.ScanRootsId), SettingRowKind.Item);
        var asked = Reduce(state, SettingsCommandKind.Remove).State;

        var outcome = Reduce(asked, SettingsCommandKind.ConfirmYes);

        Assert.Empty(outcome.Commit!.Value.Paths);
        Assert.Equal(-1, outcome.Commit.SelectItem);
        Assert.Equal("[]", outcome.Commit.Value.ToJson(SettingControl.PathList));
    }

    [Fact]
    public void GivenFolders_WhenReordering_ThenTheyTradePlacesAndTheEndsStay()
    {
        var state = Open("{ \"scanRoots\": [\"/a\", \"/b\", \"/c\"] }");
        var definition = Definition(SettingsRegistry.ScanRootsId);

        var down = Reduce(state.Reveal(definition, SettingRowKind.Item), SettingsCommandKind.ItemDown);
        var up = Reduce(state.Reveal(definition, SettingRowKind.Item, 2), SettingsCommandKind.ItemUp);
        var topUp = Reduce(state.Reveal(definition, SettingRowKind.Item), SettingsCommandKind.ItemUp);
        var bottomDown = Reduce(state.Reveal(definition, SettingRowKind.Item, 2), SettingsCommandKind.ItemDown);

        Assert.Equal(["/b", "/a", "/c"], down.Commit?.Value.Paths);
        Assert.Equal(1, down.Commit?.SelectItem);
        Assert.Equal(["/a", "/c", "/b"], up.Commit?.Value.Paths);
        Assert.Null(topUp.Commit);
        Assert.Null(bottomDown.Commit);
    }

    [Fact]
    public void GivenAFilter_WhenTypingText_ThenSettingsOfEveryCategoryMatchByNameOrHelp()
    {
        var state = Reduce(Open(), SettingsCommandKind.FilterOpen).State;
        Assert.True(state.IsEditingFilter);

        var byName = state.WithFilterText("frame");
        var byHelp = state.WithFilterText("PICKER");
        var none = state.WithFilterText("zzz");

        Assert.Equal([SettingsRegistry.FrameStyleId], byName.Rows.Select(row => row.Definition.Id));
        Assert.Equal([SettingsRegistry.ScanRootsId], byHelp.Rows.Select(row => row.Definition.Id));
        Assert.Empty(none.Rows);
    }

    [Fact]
    public void GivenAcceptedFilter_WhenActivatingAMatch_ThenTheCursorJumpsToTheSettingInItsCategoryAndTheFilterClears()
    {
        var typing = Reduce(Open(), SettingsCommandKind.FilterOpen).State.WithFilterText("branch");
        var accepted = Reduce(typing, SettingsCommandKind.InputAccept).State;
        Assert.False(accepted.IsTyping);
        Assert.True(accepted.IsFiltering);

        var jumped = Reduce(accepted, SettingsCommandKind.Activate).State;

        Assert.False(jumped.IsFiltering);
        Assert.Equal(SettingsColumn.Settings, jumped.Column);
        Assert.Equal(SettingCategory.Tabs, jumped.CurrentCategory);
        Assert.Equal(SettingsRegistry.ShowBranchId, jumped.Selected?.Definition.Id);
    }

    [Fact]
    public void GivenTheFilterBox_WhenGoingBack_ThenTheFilterClearsAndTheScreenStaysOpen()
    {
        var typing = Reduce(Open(), SettingsCommandKind.FilterOpen).State.WithFilterText("frame");

        var outcome = Reduce(typing, SettingsCommandKind.Back);

        Assert.False(outcome.Closes);
        Assert.False(outcome.State.IsEditingFilter);
        Assert.False(outcome.State.IsFiltering);
        Assert.Equal(SettingsColumn.Categories, outcome.State.Column);
    }

    [Fact]
    public void GivenAcceptedFilter_WhenGoingBack_ThenTheFilterClearsAndTheSelectedSettingStaysSelected()
    {
        var typing = Reduce(Open(), SettingsCommandKind.FilterOpen).State.WithFilterText("scan");
        var accepted = Reduce(typing, SettingsCommandKind.InputAccept).State;

        var outcome = Reduce(accepted, SettingsCommandKind.Back);

        Assert.False(outcome.State.IsFiltering);
        Assert.Equal(SettingsRegistry.ScanRootsId, outcome.State.Selected?.Definition.Id);
    }

    [Fact]
    public void GivenTheSettingsColumn_WhenGoingBackTwice_ThenTheCategoriesGetTheCursorAndThenTheScreenCloses()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.FrameStyleId));

        var first = Reduce(state, SettingsCommandKind.Back);
        var second = Reduce(first.State, SettingsCommandKind.Back);

        Assert.Equal(SettingsColumn.Categories, first.State.Column);
        Assert.False(first.Closes);
        Assert.True(second.Closes);
    }

    [Fact]
    public void GivenTheCollapsedLayout_WhenGoingBackFromTheList_ThenTheScreenCloses()
    {
        var state = Open().WithLayout(collapsed: true);

        var outcome = Reduce(state, SettingsCommandKind.Back);

        Assert.True(outcome.Closes);
    }

    [Fact]
    public void GivenQuit_WhenReducing_ThenTheScreenClosesWhateverIsOpen()
    {
        var typing = Reduce(Open().Reveal(Definition(SettingsRegistry.ScanRootsId)), SettingsCommandKind.Add).State;

        Assert.True(Reduce(typing, SettingsCommandKind.Close).Closes);
    }

    [Fact]
    public void GivenAFileThatCannotBeEdited_WhenChangingAnything_ThenTheBannerIsTheRefusalAndNothingIsCommitted()
    {
        var state = Open("{ oops").Reveal(Definition(SettingsRegistry.ShowBranchId));
        Assert.True(state.IsReadOnly);

        var toggle = Reduce(state, SettingsCommandKind.Toggle);
        var activate = Reduce(state, SettingsCommandKind.Activate);
        var add = Reduce(state.Reveal(Definition(SettingsRegistry.ScanRootsId)), SettingsCommandKind.Add);

        Assert.Null(toggle.Commit);
        Assert.Contains("cannot be edited", toggle.Warning, StringComparison.Ordinal);
        Assert.Null(activate.Commit);
        Assert.Equal(state.Banner, activate.Warning);
        Assert.Null(add.State.Input);
        Assert.Equal(state.Banner, add.Warning);
    }

    [Fact]
    public void GivenAFileThatCannotBeEdited_WhenActivatingTheEditorRow_ThenTheEditorStillOpens()
    {
        var state = Open("{ oops").Reveal(Definition(SettingsRegistry.OpenConfigId));

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.True(outcome.OpensEditor);
    }

    [Fact]
    public void GivenTheEditorRow_WhenActivating_ThenTheEditorIsRequested()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.OpenConfigId));

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.True(outcome.OpensEditor);
        Assert.Null(outcome.Commit);
    }

    [Fact]
    public void GivenTheCollapsedLayout_WhenSwitchingToIt_ThenEveryCategoryIsListedAndTheSelectionStays()
    {
        var state = Open().Reveal(Definition(SettingsRegistry.ShowBranchId));

        var collapsed = state.WithLayout(collapsed: true);
        var expanded = collapsed.WithLayout(collapsed: false);

        Assert.Equal(6, collapsed.Rows.Count);
        Assert.Equal(SettingsRegistry.ShowBranchId, collapsed.Selected?.Definition.Id);
        Assert.Equal(SettingsRegistry.ShowBranchId, expanded.Selected?.Definition.Id);
        Assert.Equal(SettingCategory.Tabs, expanded.CurrentCategory);
    }

    [Fact]
    public void GivenHalfFinishedEdits_WhenTheFileIsLoadedAgain_ThenThoseEditsAreDropped()
    {
        var state = Reduce(Open().Reveal(Definition(SettingsRegistry.ScanRootsId)), SettingsCommandKind.Add).State;
        Assert.NotNull(state.Input);

        var reloaded = state.WithLoad(Load("{ \"scanRoots\": [\"/a\"] }"));

        Assert.Null(reloaded.Input);
        Assert.Equal(["/a"], reloaded.ValueOf(Definition(SettingsRegistry.ScanRootsId)).Paths);
    }

    [Fact]
    public void GivenFilterOpenWhileTyping_WhenPressingFilterAgain_ThenNothingChanges()
    {
        var typing = Reduce(Open().Reveal(Definition(SettingsRegistry.ScanRootsId)), SettingsCommandKind.Add).State;

        var outcome = Reduce(typing, SettingsCommandKind.FilterOpen);

        Assert.Equal(typing, outcome.State);
    }

    private static SettingsState OpenChoice() =>
        Reduce(Open().Reveal(Definition(SettingsRegistry.FrameStyleId)), SettingsCommandKind.Activate).State;

    [Fact]
    public void GivenAKeyReferenceRow_WhenActivating_ThenTheStatusShowsItsKeysEntryAndNothingIsWritten()
    {
        var state = WithReference("{ \"banner\": 5 }").Reveal(
            Definition(SettingsRegistry.KeyReferenceId),
            SettingRowKind.Binding,
            1);

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.Equal(
            "Down, config.json keys entry: { \"view\": \"Graph\", \"key\": \"j\", \"action\": \"list.down\" }",
            outcome.Notice);
        Assert.Null(outcome.Commit);
        Assert.Equal(string.Empty, outcome.Warning);
    }

    [Fact]
    public void GivenAReadOnlyFile_WhenActivatingAKeyReferenceRow_ThenTheEntryIsStillShown()
    {
        var state = WithReference("not json").Reveal(
            Definition(SettingsRegistry.KeyReferenceId),
            SettingRowKind.Binding);

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.True(state.IsReadOnly);
        Assert.NotEqual(string.Empty, outcome.Notice);
        Assert.Equal(string.Empty, outcome.Warning);
    }

    [Theory]
    [InlineData(SettingsCommandKind.Toggle)]
    [InlineData(SettingsCommandKind.Add)]
    [InlineData(SettingsCommandKind.Remove)]
    [InlineData(SettingsCommandKind.ItemDown)]
    [InlineData(SettingsCommandKind.Increase)]
    public void GivenAKeyReferenceRow_WhenEditing_ThenNothingChanges(SettingsCommandKind command)
    {
        var state = WithReference("{}").Reveal(
            Definition(SettingsRegistry.KeyReferenceId),
            SettingRowKind.Binding);

        var outcome = Reduce(state, command);

        Assert.Equal(state, outcome.State);
        Assert.Null(outcome.Commit);
    }

    [Fact]
    public void GivenAKeyReferenceSetting_WhenActivatingItsOwnRow_ThenNothingHappens()
    {
        var state = WithReference("{}").Reveal(Definition(SettingsRegistry.KeyReferenceId));

        var outcome = Reduce(state, SettingsCommandKind.Activate);

        Assert.Equal(state, outcome.State);
        Assert.Equal(string.Empty, outcome.Notice);
    }

    [Fact]
    public void GivenAKeyReference_WhenListingRows_ThenEachLineIsABindingRowUnderItsSetting()
    {
        var state = WithReference("{}").Reveal(Definition(SettingsRegistry.KeyReferenceId));

        var rows = state.Rows;

        Assert.Equal(SettingRowKind.Setting, rows[0].Kind);
        Assert.Equal(3, rows.Count(row => row.Kind == SettingRowKind.Binding));
    }

    [Theory]
    [InlineData("QUIT", "app.quit")]
    [InlineData("list.up", "list.up")]
    [InlineData("j / down", "list.down")]
    public void GivenAKeyReference_WhenFilteringByTitleKeysOrActionId_ThenTheLineIsListed(string text, string action)
    {
        var state = WithReference("{}").WithFilterText(text);

        var bindings = state.Rows.Where(row => row.Kind == SettingRowKind.Binding).ToList();

        Assert.Equal([action], bindings.Select(row => state.Reference[row.Index].ActionId));
    }

    [Fact]
    public void GivenAFilterMatchingNoKeyLine_WhenListingRows_ThenTheKeyReferenceIsHidden()
    {
        var state = WithReference("{}").WithFilterText("zzz");

        Assert.Empty(state.Rows);
    }

    [Fact]
    public void GivenAFilteredKeyLine_WhenActivating_ThenTheCursorJumpsToThatLineInTheReference()
    {
        var typing = Reduce(WithReference("{}"), SettingsCommandKind.FilterOpen).State.WithFilterText("list.up");
        var accepted = Reduce(typing, SettingsCommandKind.InputAccept).State;
        Assert.Equal(SettingRowKind.Setting, accepted.Rows[0].Kind);
        var onLine = accepted.MoveTo(1);

        var jumped = Reduce(onLine, SettingsCommandKind.Activate).State;

        Assert.False(jumped.IsFiltering);
        Assert.Equal(SettingRowKind.Binding, jumped.Selected?.Kind);
        Assert.Equal("list.up", jumped.Reference[jumped.Selected!.Index].ActionId);
    }

    [Fact]
    public void GivenTheCollapsedLayout_WhenListingRows_ThenTheKeyReferenceComesLast()
    {
        var rows = WithReference("{}").WithLayout(collapsed: true).Rows;

        Assert.Equal(SettingsRegistry.KeyReferenceId, rows[^4].Definition.Id);
        Assert.DoesNotContain(rows.Take(rows.Count - 4), row => row.Definition.Id == SettingsRegistry.KeyReferenceId);
        Assert.Equal(SettingsRegistry.OpenConfigId, rows[^5].Definition.Id);
    }

    private static SettingsState WithReference(string text) =>
        SettingsState.Create(
            [.. SettingsRegistry.All],
            Load(text),
            [
                new KeyReferenceEntry("Global", "q", "Quit", "app.quit", "{ }"),
                new KeyReferenceEntry(
                    "Graph",
                    "j / Down",
                    "Down",
                    "list.down",
                    "{ \"view\": \"Graph\", \"key\": \"j\", \"action\": \"list.down\" }"),
                new KeyReferenceEntry("Graph", "k", "Up", "list.up", "{ }"),
            ]);

    private static SettingsOutcome Reduce(SettingsState state, SettingsCommandKind command) =>
        SettingsReducer.Reduce(state, command, Environment);

    private static SettingDefinition Definition(string id) =>
        SettingsRegistry.All.Concat([PageSize]).First(definition => definition.Id == id);

    private static SettingsLoad Load(string text) =>
        SettingsValueReader.Read(new ConfigSnapshot(true, text, "hash"), Home, Environment.DirectoryExists);

    private static SettingsState Open(string text = "{}") =>
        SettingsState.Create([.. SettingsRegistry.All], Load(text));

    private static SettingsState WithPageSize()
    {
        var definitions = SettingsRegistry.All.Concat([PageSize]).ToList();
        var state = SettingsState.Create(definitions, Load("{}")).WithNumber(PageSizeId, 10);

        return state.Reveal(PageSize);
    }
}

internal static class SettingsStateTestExtensions
{
    public static SettingsState WithNumber(this SettingsState state, string id, int number) =>
        state with { Values = state.Values.SetItem(id, SettingValue.OfNumber(number)) };
}
