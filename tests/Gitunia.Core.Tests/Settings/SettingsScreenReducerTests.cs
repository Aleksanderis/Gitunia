using Gitunia.Core.Config.Models;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

// The settings overlay around its edits: file work as effects, never two at once, the key mode and the theme on screen.
public class SettingsScreenReducerTests
{
    private static readonly SettingsEnvironment Environment = new(Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "home", "user"), static _ => true);

    [Fact]
    public void GivenTheScreenIsClosed_WhenTheOpenKeyIsPressed_ThenTheFileIsReadAndNothingElseHappens()
    {
        var update = Reduce(Closed(), new SettingsActionPressed(ActionIds.SettingsOpen, null));

        var read = Assert.IsType<ReadSettingsEffect>(Assert.Single(update.Effects));
        Assert.Equal(SettingsReadIntent.Open, read.Intent);
        Assert.True(update.Session.IsBusy);
        Assert.False(update.Session.IsOpen);
    }

    [Fact]
    public void GivenTheThemeAction_WhenOpening_ThenTheReadAsksForTheCursorOnTheTheme()
    {
        var update = Reduce(Closed(), new SettingsActionPressed(ActionIds.SettingsOpenTheme, null));

        Assert.Equal(SettingsReadIntent.OpenAtTheme,
            Assert.IsType<ReadSettingsEffect>(Assert.Single(update.Effects)).Intent);
    }

    [Fact]
    public void GivenAReadInFlight_WhenOpeningAgain_ThenNothingHappens()
    {
        var busy = Closed() with { IsBusy = true };

        var update = Reduce(busy, new SettingsActionPressed(ActionIds.SettingsOpen, null));

        Assert.Empty(update.Effects);
        Assert.Equal(busy, update.Session);
    }

    [Fact]
    public void GivenTheFile_WhenItIsRead_ThenTheScreenOpensAndTheOptionsAndThemeOfTheFileAreApplied()
    {
        var update = Reduce(Busy(), Read(SettingsReadIntent.Open, "{ \"tabs\": { \"showBranch\": true } }"));

        Assert.True(update.Session.IsOpen);
        Assert.False(update.Session.IsBusy);
        var options = update.Effects.OfType<ApplySettingsOptionsEffect>().Single();
        Assert.True(options.Options.ShowBranchInTab);
        Assert.Equal(ThemeChoice.Auto, update.Effects.OfType<ApplySettingsThemeEffect>().Single().Theme);
        Assert.Empty(update.Effects.OfType<SetKeyModeEffect>());
    }

    [Fact]
    public void GivenAFileThatDoesNotParse_WhenItIsRead_ThenTheScreenIsReadOnlyAndNothingIsApplied()
    {
        var update = Reduce(Busy(), Read(SettingsReadIntent.Open, "{ not json"));

        Assert.True(update.Session.Screen!.IsReadOnly);
        Assert.Empty(update.Effects.OfType<ApplySettingsOptionsEffect>());
        Assert.Empty(update.Effects.OfType<ApplySettingsThemeEffect>());
    }

    [Fact]
    public void GivenTheOpenAtThemeIntent_WhenTheFileIsRead_ThenTheCursorIsOnTheThemeRow()
    {
        var update = Reduce(Busy(), Read(SettingsReadIntent.OpenAtTheme, string.Empty));

        Assert.Equal(SettingsRegistry.ThemeId, update.Session.Screen!.Selected!.Definition.Id);
    }

    [Fact]
    public void GivenANarrowViewport_WhenTheFileIsRead_ThenTheScreenOpensCollapsed()
    {
        var narrow = Busy() with { Viewport = new SettingsViewport(5, true) };

        var update = Reduce(narrow, Read(SettingsReadIntent.Open, string.Empty));

        Assert.True(update.Session.Screen!.IsCollapsed);
    }

    [Fact]
    public void GivenTheViewportChanges_WhenTheScreenIsOpen_ThenItCollapsesAndExpandsAndTheSelectionStays()
    {
        var open = Open();
        var selected = open.Screen!.Selected!.Definition.Id;

        var narrow = Reduce(open, new SettingsViewportChanged(new SettingsViewport(5, true))).Session;
        var wide = Reduce(narrow, new SettingsViewportChanged(new SettingsViewport(15, false))).Session;

        Assert.True(narrow.Screen!.IsCollapsed);
        Assert.Equal(selected, narrow.Screen.Selected!.Definition.Id);
        Assert.False(wide.Screen!.IsCollapsed);
        Assert.Equal(15, wide.Viewport.PageRows);
    }

    [Fact]
    public void GivenTheScreenIsClosed_WhenCyclingTheCommandLevel_ThenTheFileIsReadForTheNextLevel()
    {
        var update = Reduce(Closed(), new SettingsActionPressed(ActionIds.ViewCycleCommandLevel, null));

        Assert.Equal(
            SettingsReadIntent.CycleCommandLevel,
            Assert.IsType<ReadSettingsEffect>(Assert.Single(update.Effects)).Intent);
        Assert.True(update.Session.IsBusy);
    }

    [Fact]
    public void GivenASaveInFlight_WhenCyclingTheCommandLevel_ThenItIsRefusedWithAMessage()
    {
        var update = Reduce(Closed() with { IsBusy = true },
            new SettingsActionPressed(ActionIds.ViewCycleCommandLevel, null));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(SettingsScreenReducer.StillSavingNotice, post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenTheLevelIsRead_WhenCycling_ThenTheNextLevelIsWrittenAgainstTheFileThatWasRead()
    {
        var read = Read(SettingsReadIntent.CycleCommandLevel, "{ \"commandLevel\": \"advanced\" }");

        var update = Reduce(Busy(), read);

        var write = Assert.IsType<WriteSettingEffect>(Assert.Single(update.Effects));
        Assert.Equal("\"advanced+\"", write.Json);
        Assert.Equal(read.Read.Snapshot, write.Snapshot);
        Assert.True(update.Session.IsBusy);
        Assert.False(update.Session.IsOpen);
    }

    [Fact]
    public void GivenTheLevelWasWritten_WhenTheAnswerArrives_ThenItIsAppliedAndNamed()
    {
        var read = Read(SettingsReadIntent.CycleCommandLevel, "{ \"commandLevel\": \"advanced\" }");
        var writing = Reduce(Busy(), read);
        var write = writing.Effects.OfType<WriteSettingEffect>().Single();
        var after = new SettingsRead(read.Read.Snapshot,
            read.Read.Load with { Options = read.Read.Load.Options with { CommandLevel = CommandLevel.AdvancedPlus } });

        var update = Reduce(writing.Session, Written(write, ConfigWriteStatus.Written, after));

        Assert.Equal(CommandLevel.AdvancedPlus,
            update.Effects.OfType<ApplySettingsOptionsEffect>().Single().Options.CommandLevel);
        Assert.Equal("Command level: Advanced+",
            Posts(update).Single(post => post.Severity == StatusSeverity.Info).Text);
        Assert.False(update.Session.IsBusy);
    }

    [Fact]
    public void GivenAToggleRow_WhenActivatedTwice_ThenTheChangeIsWrittenAndASecondOneWhileSavingIsRefused()
    {
        var open = Open();
        var onToggle = Reduce(open, new SettingsActionPressed(ActionIds.ListDown, 4)).Session;
        var entered = Reduce(onToggle, new SettingsActionPressed(ActionIds.SettingsActivate, null)).Session;

        var first = Reduce(entered, new SettingsActionPressed(ActionIds.SettingsToggle, null));
        var second = Reduce(first.Session, new SettingsActionPressed(ActionIds.SettingsToggle, null));

        var write = Assert.IsType<WriteSettingEffect>(Assert.Single(first.Effects));
        Assert.Equal("true", write.Json);
        Assert.Equal(SettingsRegistry.ShowBranchId, write.Commit.Definition.Id);
        Assert.Equal(SettingsScreenReducer.StillSavingNotice, Assert.Single(Posts(second)).Text);
        Assert.DoesNotContain(second.Effects, effect => effect is WriteSettingEffect);
    }

    [Fact]
    public void GivenAWriteThatFinds_TheFileChanged_ThenTheNewValuesShowAndTheWarningIsPosted()
    {
        var open = Open();
        var write = new WriteSettingEffect(new SettingsCommit(SettingsRegistry.All[3], SettingValue.OfFlag(true)),
            "true", open.Snapshot);
        var fresh = new SettingsRead(open.Snapshot, Parse("{ \"tabs\": { \"showBranch\": true } }").Load);

        var update = Reduce(open with { IsBusy = true }, new SettingsWritten(write.Commit,
            Result(ConfigWriteStatus.Stale, "changed on disk"), fresh));

        Assert.Equal("changed on disk", Assert.Single(Posts(update)).Text);
        Assert.True(update.Session.Screen!.ValueOf(SettingsRegistry.All[3]).Flag);
        Assert.False(update.Session.IsBusy);
    }

    [Fact]
    public void GivenARefusedWrite_WhenTheAnswerArrives_ThenTheWarningIsPostedAndNothingIsApplied()
    {
        var open = Open();
        var commit = new SettingsCommit(SettingsRegistry.All[3], SettingValue.OfFlag(true));

        var update = Reduce(open with { IsBusy = true }, new SettingsWritten(commit, Result(ConfigWriteStatus.Refused,
            "cannot edit"), null));

        var post = Assert.Single(Posts(update));
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.Empty(update.Effects.OfType<ApplySettingsOptionsEffect>());
    }

    [Fact]
    public void GivenAFailedFileOperation_WhenItsResultArrives_ThenTheErrorIsPostedAndTheNextOneCanRun()
    {
        var update = Reduce(Busy() with { Pending = ThemeChoice.Light },
            new SettingsOperationFailed("Could not read config.json: boom"));

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
        Assert.False(update.Session.IsBusy);
        Assert.Null(update.Session.Pending);
    }

    [Fact]
    public void GivenACancelledFileOperation_WhenItsResultArrives_ThenTheNextOneCanRun()
    {
        var update = Reduce(Busy(), new SettingsOperationCancelled());

        Assert.False(update.Session.IsBusy);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAThemeRow_WhenMovingTheCursorInItsList_ThenEachThemeIsPreviewedAndEscapeRestoresTheFileOne()
    {
        var themes = OpenThemeList();

        var down = Reduce(themes, new SettingsActionPressed(ActionIds.ListDown, null));
        var back = Reduce(down.Session, new SettingsActionPressed(ActionIds.NavigateBack, null));

        Assert.Equal(
            ThemeChoice.FromText("catppuccin-mocha"),
            down.Effects.OfType<ApplySettingsThemeEffect>().Single().Theme);
        Assert.Equal(ThemeChoice.Auto, back.Effects.OfType<ApplySettingsThemeEffect>().Single().Theme);
        Assert.Empty(down.Effects.OfType<WriteSettingEffect>());
    }

    [Fact]
    public void GivenAPreviewedTheme_WhenTheScreenCloses_ThenThePreviousThemeIsBack()
    {
        var themes = Reduce(OpenThemeList(), new SettingsActionPressed(ActionIds.ListDown, 2)).Session;

        var update = Reduce(themes, new SettingsActionPressed(ActionIds.AppQuit, null));

        Assert.False(update.Session.IsOpen);
        Assert.Equal(ThemeChoice.Auto, update.Effects.OfType<ApplySettingsThemeEffect>().Single().Theme);
    }

    [Fact]
    public void GivenAPreviewedTheme_WhenKeptAndTheWriteIsRefused_ThenThePreviousThemeIsRestored()
    {
        var themes = Reduce(OpenThemeList(), new SettingsActionPressed(ActionIds.ListDown, null)).Session;

        var kept = Reduce(themes, new SettingsActionPressed(ActionIds.SettingsActivate, null));
        var write = kept.Effects.OfType<WriteSettingEffect>().Single();
        var refused = Reduce(kept.Session, new SettingsWritten(write.Commit, Result(ConfigWriteStatus.Stale,
            "changed on disk"), null));

        Assert.Equal("\"catppuccin-mocha\"", write.Json);
        Assert.Equal(kept.Session.Pending, ThemeChoice.FromText("catppuccin-mocha"));
        Assert.Equal(ThemeChoice.Auto, refused.Effects.OfType<ApplySettingsThemeEffect>().Single().Theme);
    }

    [Fact]
    public void GivenAFolderBox_WhenItOpensAndEscapeIsPressed_ThenInsertModeComesAndGoes()
    {
        var onRoots = Open(scanRoots: true);
        var entered = Reduce(onRoots, new SettingsActionPressed(ActionIds.SettingsActivate, null)).Session;

        var adding = Reduce(entered, new SettingsActionPressed(ActionIds.SettingsAdd, null));
        var cancelled = Reduce(adding.Session, new SettingsActionPressed(ActionIds.Cancel, null));

        Assert.Equal(KeyMode.Insert, adding.Effects.OfType<SetKeyModeEffect>().Single().Mode);
        Assert.Equal(KeyMode.Normal, cancelled.Effects.OfType<SetKeyModeEffect>().Single().Mode);
        Assert.Empty(cancelled.Effects.OfType<WriteSettingEffect>());
    }

    [Fact]
    public void GivenTypedText_WhenTheFolderBoxTakesIt_ThenOnlyAnOpenBoxEditsAndAKeyWithoutOneIsIgnored()
    {
        var open = Open();
        var closedBox = Reduce(open, new SettingsKeyTyped(new KeyEvent("x")));
        var adding = AddingFolder();

        var typed = Reduce(adding, new SettingsKeyTyped(new KeyEvent("a")));

        Assert.Same(open.Screen, closedBox.Session.Screen);
        Assert.Equal("a", typed.Session.Screen!.Input!.Text);
        Assert.True(typed.Session.TakesText);
    }

    [Fact]
    public void GivenTheEditorRow_WhenActivated_ThenTheEditorIsOpenedAndAReturnReadsTheFileAgain()
    {
        var onEditor = Reduce(Open(), new SettingsActionPressed(ActionIds.ListDown, 5)).Session;
        var entered = Reduce(onEditor, new SettingsActionPressed(ActionIds.SettingsActivate, null)).Session;

        var opened = Reduce(entered, new SettingsActionPressed(ActionIds.SettingsActivate, null));
        var returned = Reduce(opened.Session, new SettingsEditorClosed());

        Assert.IsType<OpenConfigEditorEffect>(Assert.Single(opened.Effects));
        Assert.Equal(SettingsReadIntent.Reload,
            Assert.IsType<ReadSettingsEffect>(Assert.Single(returned.Effects)).Intent);
        Assert.True(returned.Session.IsBusy);
    }

    [Fact]
    public void GivenTheEditorClosesWhileBusy_WhenReturning_ThenNoSecondReadStarts()
    {
        var update = Reduce(Open() with { IsBusy = true }, new SettingsEditorClosed());

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenTheReloadedFile_WhenItIsRead_ThenTheValuesAndOptionsAreAppliedAndTheNoticeIsPosted()
    {
        var update = Reduce(Open() with { IsBusy = true }, Read(SettingsReadIntent.Reload,
            "{ \"tabs\": { \"showBranch\": true } }"));

        Assert.True(update.Effects.OfType<ApplySettingsOptionsEffect>().Single().Options.ShowBranchInTab);
        Assert.Equal(SettingsScreenReducer.ReloadedNotice, Assert.Single(Posts(update)).Text);
        Assert.True(update.Session.Screen!.ValueOf(SettingsRegistry.All[3]).Flag);
    }

    [Fact]
    public void GivenTheScreenOpen_WhenOtherActionsArrive_ThenTheScreenConsumesThemAndAClosedOneOnlyItsOwn()
    {
        Assert.True(SettingsScreenReducer.Consumes(ActionIds.ListDown, true));
        Assert.False(SettingsScreenReducer.Consumes(ActionIds.ListDown, false));
        Assert.True(SettingsScreenReducer.Consumes(ActionIds.SettingsOpen, false));
        Assert.True(SettingsScreenReducer.Consumes(ActionIds.SettingsOpenTheme, false));
        Assert.True(SettingsScreenReducer.Consumes(ActionIds.ViewCycleCommandLevel, false));
    }

    [Fact]
    public void GivenTheOpenKey_WhenPressedAgainOrQuitting_ThenTheScreenCloses()
    {
        Assert.False(Reduce(Open(), new SettingsActionPressed(ActionIds.SettingsOpen, null)).Session.IsOpen);
        Assert.False(Reduce(Open(), new SettingsActionPressed(ActionIds.AppQuit, null)).Session.IsOpen);
    }

    [Fact]
    public void GivenAClickOnACategoryAndARow_WhenSelecting_ThenTheCursorMovesWithoutEditing()
    {
        var open = Open();

        var category = Reduce(open, new SettingsCategoryClicked(IndexOf(open, SettingCategory.Tabs))).Session;
        var row = Reduce(category, new SettingsRowClicked(0));

        Assert.Equal(SettingCategory.Tabs, category.Screen!.CurrentCategory);
        Assert.Equal(0, row.Session.Screen!.Cursor);
        Assert.Equal(SettingsColumn.Settings, row.Session.Screen.Column);
        Assert.Empty(row.Effects.OfType<WriteSettingEffect>());
    }

    [Fact]
    public void GivenTheWheelOverEachColumn_WhenFocusing_ThenTheColumnUnderThePointerGetsTheCursor()
    {
        var open = Open();

        var categories = Reduce(open, new SettingsColumnFocused(SettingsColumn.Categories)).Session;
        var settings = Reduce(categories, new SettingsColumnFocused(SettingsColumn.Settings)).Session;

        Assert.Equal(SettingsColumn.Categories, categories.Screen!.Column);
        Assert.Equal(SettingsColumn.Settings, settings.Screen!.Column);
    }

    [Fact]
    public void GivenATypedFolder_WhenAccepting_ThenTheDiskCheckIsAnEffectAndTheFileStaysBusyUntilTheAnswer()
    {
        var typing = Typed(AddingFolder(), Path.Combine(Environment.HomeDirectory, "code"));

        var update = Reduce(typing, new SettingsActionPressed(ActionIds.InputAccept, null));

        var check = Assert.IsType<CheckSettingsFolderEffect>(update.Effects.OfType<SettingsEffect>().Single()).Check;
        Assert.Equal(Path.Combine(Environment.HomeDirectory, "code"), check.Path);
        Assert.Empty(update.Effects.OfType<WriteSettingEffect>());
        Assert.True(update.Session.IsBusy);
    }

    [Fact]
    public void GivenAFolderCheckInFlight_WhenAcceptingAgain_ThenNothingIsStartedAndTheUserIsTold()
    {
        var typing = Typed(AddingFolder(), Path.Combine(Environment.HomeDirectory, "code"));
        var busy = Reduce(typing, new SettingsActionPressed(ActionIds.InputAccept, null)).Session;

        var update = Reduce(busy, new SettingsActionPressed(ActionIds.InputAccept, null));

        Assert.Empty(update.Effects.OfType<SettingsEffect>());
        Assert.Equal(SettingsScreenReducer.StillSavingNotice, Assert.Single(Posts(update)).Text);
    }

    [Fact]
    public void GivenTheFolderExists_WhenTheCheckIsAnswered_ThenTheChangeIsWritten()
    {
        var check = AcceptedCheck(out var busy);

        var update = Reduce(busy, new SettingsFolderChecked(check, true));

        Assert.Equal(check.Commit, update.Effects.OfType<WriteSettingEffect>().Single().Commit);
        Assert.True(update.Session.IsBusy);
    }

    [Fact]
    public void GivenTheFolderIsMissing_WhenTheCheckIsAnswered_ThenTheReasonIsShownAndTheBoxStaysOpen()
    {
        var check = AcceptedCheck(out var busy);

        var update = Reduce(busy, new SettingsFolderChecked(check, false));

        Assert.Empty(update.Effects.OfType<WriteSettingEffect>());
        var post = Assert.Single(Posts(update));
        Assert.Contains("is not an existing folder", post.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
        Assert.False(update.Session.IsBusy);
        Assert.NotNull(update.Session.Screen!.Input);
    }

    [Fact]
    public void GivenTheBoxWasClosedMeanwhile_WhenTheCheckIsAnswered_ThenTheAnswerIsDropped()
    {
        var check = AcceptedCheck(out var busy);
        var closed = Reduce(busy, new SettingsActionPressed(ActionIds.Cancel, null)).Session;

        var update = Reduce(closed, new SettingsFolderChecked(check, true));

        Assert.Empty(update.Effects.OfType<WriteSettingEffect>());
        Assert.False(update.Session.IsBusy);
    }

    [Fact]
    public void GivenAnotherBoxOpenedMeanwhile_WhenTheCheckIsAnswered_ThenTheAnswerIsDropped()
    {
        var check = AcceptedCheck(out var busy);
        var closed = Reduce(busy, new SettingsActionPressed(ActionIds.Cancel, null)).Session;
        var reopened = Reduce(closed, new SettingsActionPressed(ActionIds.SettingsAdd, null)).Session;
        var other = Typed(reopened, Path.Combine(Environment.HomeDirectory, "other"));

        var update = Reduce(other, new SettingsFolderChecked(check, false));

        Assert.Empty(update.Effects.OfType<WriteSettingEffect>());
        Assert.Empty(Posts(update));
    }

    [Fact]
    public void GivenEveryConcreteSettingsCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<SettingsCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                SettingsScreenReducer.Reduce(SettingsSession.Empty, command);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in SettingsScreenReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static SettingsUpdate Reduce(SettingsSession session, SettingsCommand command) =>
        SettingsScreenReducer.Reduce(session, command);

    private static SettingsSession Closed() => SettingsSession.Empty with { HomeDirectory = Environment.HomeDirectory };

    private static SettingsSession Busy() => Closed() with { IsBusy = true };

    private static SettingsFileRead Read(SettingsReadIntent intent, string text) =>
        new(intent, Parse(text), SettingsRegistry.All, []);

    private static SettingsRead Parse(string text)
    {
        var snapshot = new ConfigSnapshot(text.Length > 0, text, text.Length > 0 ? "hash" : string.Empty);
        var load = SettingsValueReader.Read(snapshot, Environment.HomeDirectory, Environment.DirectoryExists);

        return new SettingsRead(snapshot, load);
    }

    private static SettingsSession Open(bool scanRoots = false)
    {
        var opened = Reduce(Busy(), Read(SettingsReadIntent.Open, string.Empty)).Session;
        if (!scanRoots)
        {
            return opened;
        }

        return Reduce(opened, new SettingsCategoryClicked(IndexOf(opened, SettingCategory.Repositories))).Session;
    }

    private static int IndexOf(SettingsSession session, SettingCategory category) =>
        Enumerable.Range(0, session.Screen!.Categories.Count)
            .First(candidate => session.Screen.Categories[candidate] == category);

    private static SettingsSession OpenThemeList()
    {
        var opened = Reduce(Busy(), Read(SettingsReadIntent.OpenAtTheme, string.Empty)).Session;

        return Reduce(opened, new SettingsActionPressed(ActionIds.SettingsActivate, null)).Session;
    }

    private static SettingsSession Typed(SettingsSession session, string text) =>
        session with { Screen = session.Screen!.WithInputText(text) };

    private static SettingsFolderCheck AcceptedCheck(out SettingsSession busy)
    {
        var typing = Typed(AddingFolder(), Path.Combine(Environment.HomeDirectory, "code"));
        var update = Reduce(typing, new SettingsActionPressed(ActionIds.InputAccept, null));
        busy = update.Session;

        return update.Effects.OfType<CheckSettingsFolderEffect>().Single().Check;
    }

    private static SettingsSession AddingFolder()
    {
        var onRoots = Open(scanRoots: true);
        var entered = Reduce(onRoots, new SettingsActionPressed(ActionIds.SettingsActivate, null)).Session;

        return Reduce(entered, new SettingsActionPressed(ActionIds.SettingsAdd, null)).Session;
    }

    private static SettingsWritten Written(WriteSettingEffect write, ConfigWriteStatus status, SettingsRead after) =>
        new(write.Commit, Result(status, string.Empty), after);

    private static ConfigWriteResult Result(ConfigWriteStatus status, string message) =>
        new(status, new ConfigSnapshot(true, string.Empty, "hash"), message, false);

    private static IReadOnlyList<PostStatusEffect> Posts(SettingsUpdate update) =>
        [.. update.Effects.OfType<PostStatusEffect>()];
}
