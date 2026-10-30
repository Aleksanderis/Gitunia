using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Status;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Settings;

public sealed class SettingsControllerTests : IDisposable
{
    private readonly Harness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GivenAFileWithValues_WhenOpening_ThenTheScreenShowsThemAndTheOptionsAreApplied()
    {
        _harness.WriteConfig("{ \"tabs\": { \"showBranch\": true } }");

        await _harness.OpenAsync();

        Assert.True(_harness.Settings.IsOpen);
        Assert.NotNull(_harness.Settings.CurrentFrame());
        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.True(_harness.Applied.Last().ShowBranchInTab);
    }

    [Fact]
    public async Task GivenAFileWithComments_WhenFlippingAToggle_ThenOnlyThatKeyIsInsertedAndTheOptionsApply()
    {
        const string original = "{\n  // keep me\n  \"theme\": \"auto\"\n}\n";
        _harness.WriteConfig(original);
        await _harness.OpenAsync();

        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        var text = _harness.ReadConfig();
        Assert.StartsWith("{\n  // keep me\n  \"theme\": \"auto\",\n", text, StringComparison.Ordinal);
        Assert.Contains("\"tabs\": { \"showBranch\": true }", text, StringComparison.Ordinal);
        Assert.True(_harness.Applied.Last().ShowBranchInTab);
        Assert.Equal("Saved Show branch in tab name", _harness.Statuses.Log.Latest!.Text);
        Assert.True(_harness.Settings.State!.ValueOf(SettingsRegistry.All[3]).Flag);
    }

    [Fact]
    public async Task GivenAToggle_WhenPressingXTwice_ThenTheFileEndsUpFlippedBackAndBothWritesApplied()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();
        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();

        Assert.Contains("\"showBranch\": false", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.False(_harness.Applied.Last().ShowBranchInTab);
    }

    [Fact]
    public async Task GivenTheLabelCapsSetting_WhenChangingIt_ThenTheRunningAppGetsTheNewStyleAtOnce()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Contains("\"labelCaps\": \"angled\"", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.Equal(LabelCapStyle.Angled, _harness.Applied.Last().LabelCaps);
    }

    [Fact]
    public async Task GivenTheWhichKeySetting_WhenTurningItOff_ThenItIsWrittenAndAppliedAtOnce()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Contains("\"hints\": { \"whichKey\": \"off\" }", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.False(_harness.Applied.Last().WhichKeyPopup);
    }

    [Fact]
    public async Task GivenTheStatusBarHintsSetting_WhenChoosingMinimal_ThenItIsWrittenAndAppliedAtOnce()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Contains("\"statusBar\": \"minimal\"", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.Equal(StatusBarHintMode.Minimal, _harness.Applied.Last().StatusBarHints);
    }

    [Fact]
    public async Task GivenAChoice_WhenKeepingAnOption_ThenTheFileAndTheRunningThemeChange()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);
        Assert.Equal(string.Empty, _harness.ReadConfig());

        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Equal("{\n  \"theme\": \"catppuccin-mocha\"\n}\n", _harness.ReadConfig());
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
    }

    [Fact]
    public async Task GivenTheThemeList_WhenMovingTheCursor_ThenEachThemeIsPreviewedAndNothingIsWritten()
    {
        await _harness.OpenThemeListAsync();

        _harness.Run(ActionIds.ListDown);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        _harness.Run(ActionIds.ListDown);
        Assert.Equal(ThemeChoice.FromText("catppuccin-macchiato"), _harness.Themes.Last());
        _harness.Run(ActionIds.ListUp);

        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        Assert.False(File.Exists(_harness.ConfigPath));
    }

    [Fact]
    public async Task GivenAPreviewedTheme_WhenPressingEscape_ThenThePreviousThemeIsBackAndNothingIsWritten()
    {
        _harness.WriteConfig("{ \"theme\": \"light\" }");
        await _harness.OpenThemeListAsync();
        _harness.Run(ActionIds.ListUp);
        Assert.Equal(ThemeChoice.FromText("catppuccin-frappe"), _harness.Themes.Last());

        _harness.Run(ActionIds.NavigateBack);

        Assert.Equal(ThemeChoice.Light, _harness.Themes.Last());
        Assert.True(_harness.Settings.IsOpen);
        Assert.Equal("{ \"theme\": \"light\" }", _harness.ReadConfig());
    }

    [Fact]
    public async Task GivenAPreviewedTheme_WhenTheScreenCloses_ThenThePreviousThemeIsBack()
    {
        await _harness.OpenThemeListAsync();
        _harness.Run(ActionIds.ListDown, 2);
        Assert.Equal(ThemeChoice.FromText("catppuccin-macchiato"), _harness.Themes.Last());

        _harness.Run(ActionIds.AppQuit);

        Assert.False(_harness.Settings.IsOpen);
        Assert.Equal(ThemeChoice.Auto, _harness.Themes.Last());
        Assert.False(File.Exists(_harness.ConfigPath));
    }

    [Fact]
    public async Task GivenAPreviewedTheme_WhenPressingEnter_ThenItStaysAndIsWrittenAndReadBackAtTheNextStart()
    {
        _harness.WriteConfig("{\n  // keep me\n  \"tabs\": { \"showBranch\": false }\n}\n");
        await _harness.OpenThemeListAsync();
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.SettingsActivate);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        await _harness.PumpAsync();

        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        Assert.Equal("Saved Theme", _harness.Statuses.Log.Latest!.Text);
        Assert.Contains("// keep me", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"catppuccin-mocha\"", _harness.ReadConfig(), StringComparison.Ordinal);
        var next = new ConfigStartup(Path.GetDirectoryName(_harness.ConfigPath)!, _harness.Root).Load();
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), next.Theme);
        _harness.Run(ActionIds.AppQuit);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
    }

    [Fact]
    public async Task GivenAKeptThemeWhoseWriteIsRefused_WhenTheAnswerArrives_ThenThePreviousThemeIsRestored()
    {
        _harness.WriteConfig("{ \"theme\": \"light\" }");
        await _harness.OpenThemeListAsync();
        _harness.Run(ActionIds.ListUp);
        _harness.WriteConfig("{ \"theme\": \"light\", \"tabs\": { \"showBranch\": true } }");

        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Contains("changed on disk", _harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(ThemeChoice.Light, _harness.Themes.Last());
        Assert.Equal("{ \"theme\": \"light\", \"tabs\": { \"showBranch\": true } }", _harness.ReadConfig());
    }

    [Fact]
    public async Task GivenAFileThatDoesNotParse_WhenOpeningAndBrowsing_ThenNoThemeIsApplied()
    {
        _harness.WriteConfig("{ \"theme\": ");

        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.NavigateBack);

        Assert.Empty(_harness.Themes);
    }

    [Fact]
    public async Task GivenThePaletteActionForTheTheme_WhenOpening_ThenTheCursorIsOnTheThemeRow()
    {
        Assert.True(_harness.Settings.TryExecute(ActionIds.SettingsOpenTheme, null));
        await _harness.PumpAsync();

        var state = _harness.Settings.State!;
        Assert.Equal(SettingsRegistry.ThemeId, state.Selected!.Definition.Id);
        Assert.Equal(SettingsColumn.Settings, state.Column);
        Assert.Equal(SettingCategory.Appearance, state.CurrentCategory);
    }

    [Fact]
    public async Task GivenTheThemeList_WhenClickingOptionsWithTheMouse_ThenTheFirstClickPreviewsAndTheSecondKeeps()
    {
        await _harness.OpenThemeListAsync();

        var first = _harness.Settings.OnLineClicked(2 + 2, 30, out _);
        Assert.Null(first);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        Assert.False(File.Exists(_harness.ConfigPath));
        var second = _harness.Settings.OnLineClicked(2 + 2, 30, out _);
        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Equal(DefaultMouseMap.SettingsEditButton, second);
        Assert.Contains("\"theme\": \"catppuccin-mocha\"", _harness.ReadConfig(), StringComparison.Ordinal);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
    }

    [Fact]
    public async Task GivenAPreviewedThemeByMouse_WhenClickingAnotherRow_ThenThePreviousThemeIsBack()
    {
        await _harness.OpenThemeListAsync();
        _harness.Settings.OnLineClicked(2 + 3, 30, out _);
        Assert.Equal(ThemeChoice.FromText("catppuccin-macchiato"), _harness.Themes.Last());

        _harness.Settings.OnLineClicked(2, 30, out _);

        Assert.Equal(ThemeChoice.Auto, _harness.Themes.Last());
        Assert.Null(_harness.Settings.State!.Choice);
        Assert.False(File.Exists(_harness.ConfigPath));
    }

    [Fact]
    public async Task GivenAnOpenChoice_WhenGoingBack_ThenNothingIsWrittenAndTheScreenStaysOpen()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.NavigateBack);

        Assert.False(File.Exists(_harness.ConfigPath));
        Assert.True(_harness.Settings.IsOpen);
        Assert.Null(_harness.Settings.State!.Choice);
    }

    [Fact]
    public async Task GivenNoConfigFile_WhenMakingTheFirstChange_ThenTheDirectoryAndFileAreCreatedWithOnlyThatKey()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();

        Assert.Equal("{\n  \"tabs\": { \"showBranch\": true }\n}\n", _harness.ReadConfig());
    }

    [Fact]
    public async Task GivenAFolder_WhenAddingItByKeys_ThenItIsTypedInInsertModeSavedAndAppliedToThePicker()
    {
        var folder = _harness.MakeFolder("projects");
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 2);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsAdd);
        Assert.Equal(KeyMode.Insert, _harness.Mode);
        _harness.Type(folder);
        _harness.Run(ActionIds.InputAccept);
        await _harness.PumpAsync();

        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.Equal([folder], _harness.Applied.Last().ScanRoots);
        Assert.Equal(
            $"{{\n  \"scanRoots\": [{ConfigTextEditor.StringValue(folder)}]\n}}\n",
            _harness.ReadConfig());
        Assert.Equal(SettingRowKind.Item, _harness.Settings.State!.Selected!.Kind);
    }

    [Fact]
    public async Task GivenAFolderThatDoesNotExist_WhenAddingIt_ThenTheReasonIsReportedAndTypingContinues()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 2);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsAdd);
        _harness.Type(Path.Combine(_harness.Root, "nowhere"));

        _harness.Run(ActionIds.InputAccept);

        Assert.Contains("not an existing folder", _harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Warning, _harness.Statuses.Log.Latest.Severity);
        Assert.False(File.Exists(_harness.ConfigPath));
        Assert.Equal(KeyMode.Insert, _harness.Mode);
    }

    [Fact]
    public async Task GivenTyping_WhenPressingEscape_ThenTheBoxClosesWithoutSavingAndNormalModeReturns()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 2);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsAdd);
        _harness.Type("/tm");

        _harness.Run(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.Null(_harness.Settings.State!.Input);
        Assert.True(_harness.Settings.IsOpen);
        Assert.False(File.Exists(_harness.ConfigPath));
    }

    [Fact]
    public async Task GivenTwoFolders_WhenRemovingOneAfterTheQuestion_ThenOnlyYesWritesTheShorterList()
    {
        var first = _harness.MakeFolder("a");
        var second = _harness.MakeFolder("b");
        _harness.WriteRoots(first, second);
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 2);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);
        var before = _harness.ReadConfig();

        _harness.Run(ActionIds.SettingsRemove);
        Assert.Equal(KeyMode.Confirm, _harness.Mode);
        _harness.Run(ActionIds.ConfirmNo);
        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.Equal(before, _harness.ReadConfig());
        _harness.Run(ActionIds.SettingsRemove);
        _harness.Run(ActionIds.ConfirmYes);
        await _harness.PumpAsync();

        Assert.Equal([second], _harness.Applied.Last().ScanRoots);
        Assert.Equal($"{{ \"scanRoots\": [{ConfigTextEditor.StringValue(second)}] }}", _harness.ReadConfig());
        Assert.True(Directory.Exists(first), "The folder itself is never touched.");
    }

    [Fact]
    public async Task GivenTwoFolders_WhenMovingTheFirstDown_ThenTheOrderIsSavedAndTheCursorFollows()
    {
        var first = _harness.MakeFolder("a");
        var second = _harness.MakeFolder("b");
        _harness.WriteRoots(first, second);
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 2);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.ListDown);

        _harness.Run(ActionIds.SettingsMoveDown);
        await _harness.PumpAsync();

        Assert.Equal([second, first], _harness.Applied.Last().ScanRoots);
        Assert.Equal(1, _harness.Settings.State!.Selected!.Index);
    }

    [Fact]
    public async Task GivenTheFileChangedOnDisk_WhenChangingASetting_ThenNothingIsOverwrittenAndTheNewValuesShow()
    {
        _harness.WriteConfig("{ \"tabs\": { \"showBranch\": false } }");
        await _harness.OpenAsync();
        _harness.WriteConfig("{ \"tabs\": { \"showBranch\": true } }");
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();

        Assert.Equal("{ \"tabs\": { \"showBranch\": true } }", _harness.ReadConfig());
        Assert.Contains("changed on disk", _harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.True(_harness.Settings.State!.ValueOf(SettingsRegistry.All[3]).Flag);

        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();

        Assert.Contains("\"showBranch\": false", _harness.ReadConfig(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFileThatDoesNotParse_WhenOpeningAndChanging_ThenTheScreenIsReadOnlyAndNothingIsWritten()
    {
        const string broken = "{ \"tabs\": ";
        _harness.WriteConfig(broken);
        var appliedBefore = _harness.Applied.Count;

        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsToggle);

        Assert.True(_harness.Settings.State!.IsReadOnly);
        Assert.Contains("cannot be edited", _harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(broken, _harness.ReadConfig());
        Assert.Equal(appliedBefore, _harness.Applied.Count);
    }

    [Fact]
    public async Task GivenAFilterByKeys_WhenTypingAndJumping_ThenTheCursorLandsOnTheSettingInItsCategory()
    {
        await _harness.OpenAsync();

        _harness.Run(ActionIds.SettingsFilter);
        Assert.Equal(KeyMode.Insert, _harness.Mode);
        _harness.Type("branch in tab");
        _harness.Run(ActionIds.InputAccept);
        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.Single(_harness.Settings.State!.Rows);
        _harness.Run(ActionIds.SettingsActivate);

        Assert.False(_harness.Settings.State.IsFiltering);
        Assert.Equal(SettingsRegistry.ShowBranchId, _harness.Settings.State.Selected!.Definition.Id);
        Assert.Equal(SettingsColumn.Settings, _harness.Settings.State.Column);
    }

    [Fact]
    public async Task GivenTheFilterBox_WhenBackspacingAndEscaping_ThenTheTextEditsAndEscClearsIt()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsFilter);
        _harness.Type("frames");

        _harness.Settings.HandleTextKey(new KeyEvent(KeyNames.Backspace));
        Assert.Equal("frame", _harness.Settings.State!.Filter);
        _harness.Run(ActionIds.Cancel);

        Assert.Equal(string.Empty, _harness.Settings.State.Filter);
        Assert.Equal(KeyMode.Normal, _harness.Mode);
        Assert.True(_harness.Settings.IsOpen);
    }

    [Fact]
    public async Task GivenTheScreenOpen_WhenPressingEscapeFromTheCategories_ThenItClosesAndOtherActionsPassThrough()
    {
        await _harness.OpenAsync();

        Assert.True(_harness.Settings.TryExecute(ActionIds.PaneNext, null));
        Assert.True(_harness.Settings.TryExecute(ActionIds.GraphJumpToHead, null));
        Assert.True(_harness.Settings.IsOpen);
        _harness.Run(ActionIds.NavigateBack);

        Assert.False(_harness.Settings.IsOpen);
        Assert.Null(_harness.Settings.CurrentFrame());
        Assert.False(_harness.Settings.TryExecute(ActionIds.PaneNext, null));
    }

    [Fact]
    public async Task GivenTheOpenKey_WhenPressedAgainOrQuitting_ThenTheScreenCloses()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsOpen);
        Assert.False(_harness.Settings.IsOpen);

        await _harness.OpenAsync();
        _harness.Run(ActionIds.AppQuit);
        Assert.False(_harness.Settings.IsOpen);
    }

    [Fact]
    public async Task GivenAClosedScreenWhileAnotherReadIsRunning_WhenOpeningTwice_ThenOnlyOneScreenOpens()
    {
        Assert.True(_harness.Settings.TryExecute(ActionIds.SettingsOpen, null));
        Assert.True(_harness.Settings.TryExecute(ActionIds.SettingsOpen, null));

        await _harness.PumpAsync();

        Assert.True(_harness.Settings.IsOpen);
        Assert.Equal(1, _harness.Opened);
    }

    [Fact]
    public async Task GivenTwoQuickChanges_WhenTheSecondComesBeforeTheFirstIsWritten_ThenItIsRefusedWithAMessage()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsToggle);
        _harness.Run(ActionIds.SettingsToggle);
        await _harness.PumpAsync();

        Assert.Contains(
            _harness.Statuses.Log.History,
            message => message.Text.StartsWith("Still saving", StringComparison.Ordinal));
        Assert.Contains("\"showBranch\": true", _harness.ReadConfig(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheEditorRow_WhenActivating_ThenTheEditorGetsTheConfigPathAndTheFileIsReadOnReturn()
    {
        _harness.WriteConfig("{ \"tabs\": { \"showBranch\": false } }");
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 4);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsActivate);
        Assert.Equal(_harness.ConfigPath, _harness.EditorFile);
        _harness.WriteConfig("{ \"tabs\": { \"showBranch\": true }, \"scanRoots\": [] }");
        _harness.EditorReturned!();
        await _harness.PumpAsync();

        Assert.True(_harness.Applied.Last().ShowBranchInTab);
        Assert.Contains("apply after a restart", _harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoEditorConfigured_WhenActivatingTheEditorRow_ThenTheReasonIsReported()
    {
        _harness.EditorNotice = "Set $VISUAL or $EDITOR to open config.json";
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 4);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Run(ActionIds.SettingsActivate);

        Assert.Equal("Set $VISUAL or $EDITOR to open config.json", _harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenAClickOnACategory_WhenSelectingIt_ThenTheCategoryIsSelectedWithoutEditingAnything()
    {
        await _harness.OpenAsync();

        var view = _harness.Settings.OnLineClicked(2 + 3, 3, out var handled);

        Assert.True(handled);
        Assert.Null(view);
        Assert.Equal(SettingCategory.Tabs, _harness.Settings.State!.CurrentCategory);
    }

    [Fact]
    public async Task GivenARow_WhenClickingItTwice_ThenTheFirstClickSelectsAndTheSecondEditsLikeEnter()
    {
        await _harness.OpenAsync();
        _harness.Settings.OnLineClicked(2 + 3, 3, out _);
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.NavigateBack);
        _harness.Settings.OnLineClicked(2 + 3, 3, out _);

        var first = _harness.Settings.OnLineClicked(2, 30, out _);
        var second = _harness.Settings.OnLineClicked(2, 30, out _);

        Assert.Null(first);
        Assert.Equal(DefaultMouseMap.SettingsEditButton, second);
        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();
        Assert.Contains("\"showBranch\": true", _harness.ReadConfig(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAToggleBox_WhenClickingTheControl_ThenItEditsAtOnceThroughTheMouseMapsAction()
    {
        await _harness.OpenAsync();
        _harness.Settings.OnLineClicked(2 + 3, 3, out _);

        var view = _harness.Settings.OnLineClicked(2, _harness.ControlColumn, out _);

        Assert.Equal(DefaultMouseMap.SettingsEditButton, view);
        Assert.Equal(ActionIds.SettingsActivate, DefaultMouseMap.Create().Resolve(view!, MouseGesture.Click)!.Action);
    }

    [Fact]
    public async Task GivenAPathList_WhenClickingItsMarkers_ThenTheViewsReturnAndTheQuestionIsAnsweredByClick()
    {
        var folder = _harness.MakeFolder("a");
        _harness.WriteConfig($"{{ \"scanRoots\": [{ConfigTextEditor.StringValue(folder)}] }}");
        await _harness.OpenAsync();
        _harness.Settings.OnLineClicked(2 + 2, 3, out _);
        var layout = SettingsLayout.For(100, 30)!;

        var add = _harness.Settings.OnLineClicked(2, layout.Width - 3, out _);
        var remove = _harness.Settings.OnLineClicked(3, layout.Width - 3, out _);
        _harness.Run(ActionIds.SettingsRemove);
        var yes = _harness.Settings.OnLineClicked(layout.SecondHelpLine, 5, out _);
        _harness.Run(ActionIds.ConfirmYes);
        await _harness.PumpAsync();

        Assert.Equal(DefaultMouseMap.SettingsAddButton, add);
        Assert.Equal(DefaultMouseMap.SettingsRemoveButton, remove);
        Assert.Equal(DefaultMouseMap.ConfirmYesButton, yes);
        Assert.Equal("{ \"scanRoots\": [] }", _harness.ReadConfig());
    }

    [Fact]
    public async Task GivenTheThemeOptions_WhenClickingAnOptionTwice_ThenTheFirstClickPreviewsAndTheSecondKeeps()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        _harness.Run(ActionIds.SettingsActivate);

        var first = _harness.Settings.OnLineClicked(2 + 1 + 1, 30, out _);
        Assert.Null(first);
        Assert.Equal(ThemeChoice.FromText("catppuccin-mocha"), _harness.Themes.Last());
        var second = _harness.Settings.OnLineClicked(2 + 1 + 1, 30, out _);
        _harness.Run(ActionIds.SettingsActivate);
        await _harness.PumpAsync();

        Assert.Equal(DefaultMouseMap.SettingsEditButton, second);
        Assert.Contains("\"theme\": \"catppuccin-mocha\"", _harness.ReadConfig(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTheWheelOverEachColumn_WhenFocusingByPointer_ThenTheColumnUnderThePointerGetsTheCursor()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.SettingsActivate);
        Assert.Equal(SettingsColumn.Settings, _harness.Settings.State!.Column);

        _harness.Settings.FocusColumnAt(3);
        Assert.Equal(SettingsColumn.Categories, _harness.Settings.State.Column);
        _harness.Settings.FocusColumnAt(40);

        Assert.Equal(SettingsColumn.Settings, _harness.Settings.State.Column);
    }

    [Fact]
    public async Task GivenANarrowTerminal_WhenResizing_ThenTheLayoutCollapsesAndTheSelectionStays()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown, 3);
        _harness.Run(ActionIds.SettingsActivate);

        _harness.Size = new ScreenSize(60, 24);
        _harness.Settings.OnScreenResized();

        Assert.True(_harness.Settings.State!.IsCollapsed);
        Assert.Equal(SettingsRegistry.ShowBranchId, _harness.Settings.State.Selected!.Definition.Id);
        _harness.Size = new ScreenSize(100, 30);
        _harness.Settings.OnScreenResized();
        Assert.False(_harness.Settings.State.IsCollapsed);
    }

    [Fact]
    public async Task GivenAUserRebind_WhenReadingTheKeyReference_ThenTheKeyIsListedAndEnterShowsItsEntry()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);
        var rows = _harness.Settings.State!.Rows;
        var rebound = rows.ToList().FindIndex(row => row.Kind == SettingRowKind.Binding
            && _harness.Settings.State.Reference[row.Index] is
                { Section: "Graph", Keys: "J", ActionId: "list.down" });
        Assert.True(rebound > 0, "the user's J binding is in the key reference");

        _harness.Run(ActionIds.ListDown, rebound);
        _harness.Run(ActionIds.SettingsActivate);

        Assert.Equal(
            "Move down, config.json keys entry: { \"view\": \"Graph\", \"key\": \"J\", \"action\": \"list.down\" }",
            _harness.Statuses.Log.Latest!.Text);
        Assert.False(File.Exists(_harness.ConfigPath));
    }

    [Fact]
    public async Task GivenTheKeyReference_WhenClickingAKeyTwice_ThenTheSecondClickShowsItsEntryThroughTheMouseMap()
    {
        await _harness.OpenAsync();
        _harness.Run(ActionIds.ListDown);
        _harness.Run(ActionIds.SettingsActivate);

        var first = _harness.Settings.OnLineClicked(2 + 4, 30, out _);
        var second = _harness.Settings.OnLineClicked(2 + 4, 30, out _);

        Assert.Equal(SettingRowKind.Binding, _harness.Settings.State!.Selected!.Kind);
        Assert.Null(first);
        Assert.Equal(DefaultMouseMap.SettingsEditButton, second);
    }

    private sealed class Harness : IDisposable
    {
        private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
        private int _pending;

        public Harness()
        {
            Root = Directory.CreateTempSubdirectory("gitunia-settings-").FullName;
            ConfigPath = Path.Combine(Root, "config", ConfigLoader.FileName);
            Settings = new SettingsController(
                new ConfigWriter(Path.Combine(Root, "config")),
                new SettingsEnvironment(Root, Directory.Exists),
                Statuses,
                Post,
                () => Size,
                GraphGlyphSet.Unicode,
                mode => Mode = mode,
                () => { },
                Applied.Add,
                Themes.Add,
                LaunchEditor,
                () => KeyReferenceBuilder.Build(Keymap),
                cancellationToken: TestContext.Current.CancellationToken);
        }

        public string Root { get; }

        public KeyBindingTable Keymap { get; } = KeymapConfigurator.Apply(
            DefaultKeymap.Create(),
            [new ConfigKeyEntry { View = "Graph", Key = "J", Action = "list.down" }]).Table;

        public string ConfigPath { get; }

        public SettingsController Settings { get; }

        public StatusChannel Statuses { get; } = new();

        public List<ConfigOptions> Applied { get; } = [];

        public List<ThemeChoice> Themes { get; } = [];

        public KeyMode Mode { get; private set; } = KeyMode.Normal;

        public ScreenSize Size { get; set; } = new(100, 30);

        public string EditorFile { get; private set; } = string.Empty;

        public string EditorNotice { get; set; } = string.Empty;

        public Action? EditorReturned { get; private set; }

        public int Opened => Applied.Count;

        public int ControlColumn => SettingsLayout.For(Size.Width, Size.Height)!.Width - 4;

        public void WriteConfig(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, text);
        }

        public string ReadConfig() => File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : string.Empty;

        public void WriteRoots(params string[] folders)
        {
            var list = ConfigTextEditor.StringListValue(folders);
            WriteConfig($"{{ \"scanRoots\": {list} }}");
        }

        public string MakeFolder(string name) => Directory.CreateDirectory(Path.Combine(Root, name)).FullName;

        public void Run(ActionId action, int? count = null) => Assert.True(Settings.TryExecute(action, count));

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Settings.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        // Opens the screen on the theme row and lists its options (the cursor on the current one).
        public async Task OpenThemeListAsync()
        {
            Assert.True(Settings.TryExecute(ActionIds.SettingsOpenTheme, null));
            await PumpAsync();
            Run(ActionIds.SettingsActivate);
        }

        public async Task OpenAsync()
        {
            Assert.True(Settings.TryExecute(ActionIds.SettingsOpen, null));
            await PumpAsync();
        }

        // Runs the posted results of the file work started so far (one post per read or write).
        public async Task PumpAsync()
        {
            await _pump.RunPostsAsync(Math.Max(1, _pending));
            _pending = 0;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private void Post(Action action) => _pump.Post(action);

        private string LaunchEditor(string file, Action returned)
        {
            EditorFile = file;
            EditorReturned = returned;

            return EditorNotice;
        }
    }
}
