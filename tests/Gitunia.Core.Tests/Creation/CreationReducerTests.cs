using Gitunia.Core.Creation;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Tests.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Remotes.Models;
using Xunit;

namespace Gitunia.Core.Tests.Creation;

// Clone and init from the picker through the root reducer: the questions are shell prompts, the work is effects.
public class CreationReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenOpenPicker_WhenCloneIsAsked_ThenTheSourcePromptOpensWithTheKeysInInsertMode()
    {
        var update = AppReducer.Reduce(OpenPicker(), new CreationRequested(CreationKind.Clone));
        var state = Run(update);

        var prompt = state.Shell.Prompt!;
        Assert.Equal("Clone repository", prompt.Title);
        Assert.Equal("URL or path of the repository to clone", prompt.Context);
        Assert.Equal("next", prompt.AcceptLabel);
        Assert.Equal(string.Empty, prompt.Text);
        Assert.IsType<CloneSourceAnswer>(prompt.OnAccepted);
    }

    [Fact]
    public void GivenTheLocateMode_WhenCloning_ThenItIsRefused()
    {
        var state = AppReducer.Reduce(OpenPicker(), new PickerCloseRequested()).State;
        state = AppReducer.Reduce(state, new OpenRepositoryCommand(PickerFixtures.Location("code", "gone"))).State;
        var missing = state.Workspace.Tabs[0].Id;
        state = AppReducer.Reduce(state, new PickerOpenRequested(missing, string.Empty, Start)).State;

        var update = AppReducer.Reduce(state, new CreationRequested(CreationKind.Clone));

        Assert.Equal(CreationReducer.LocatingNotice, Warning(update));
        Assert.Empty(update.Effects.OfType<ShowPromptEffect>());
    }

    [Fact]
    public void GivenClosedPicker_WhenCloneIsAsked_ThenNothingHappens()
    {
        var update = AppReducer.Reduce(Closed(), new CreationRequested(CreationKind.Clone));

        Assert.Empty(update.Effects);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GivenARunningCreation_WhenAnotherIsAsked_ThenItIsRefusedAsBusy(bool clone)
    {
        var state = OpenPicker();
        var session = clone ? new CreationSession(new CloneState(null, false), false) : new CreationSession(null, true);
        state = state with { Shell = state.Shell with { Creation = session } };

        var update = AppReducer.Reduce(state, new CreationRequested(CreationKind.Init));

        Assert.Equal(CreationReducer.BusyNotice, Warning(update));
    }

    [Fact]
    public void GivenAnEmptySource_WhenAccepting_ThenItAsksAgainWithAWarning()
    {
        var update = AppReducer.Reduce(OpenPicker(), new CloneSourceAnswered("  "));

        Assert.Equal(CreationReducer.EmptyNotice, Warning(update));
        var request = Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request;
        Assert.Equal("  ", request.InitialText);
        Assert.IsType<CloneSourceAnswer>(request.OnAccepted);
    }

    [Fact]
    public void GivenASource_WhenAccepting_ThenTheDefaultFolderIsLookedUp()
    {
        var update = AppReducer.Reduce(OpenPicker(), new CloneSourceAnswered("https://example.com/team/tool.git"));

        var resolve = Assert.Single(update.Effects.OfType<ResolveCloneParentEffect>());
        Assert.Equal("https://example.com/team/tool.git", resolve.Source);
        Assert.Null(resolve.TypedPath);
        Assert.Equal(PickerFixtures.Home, resolve.HomeDirectory);
    }

    [Fact]
    public void GivenAFolderTypedInThePicker_WhenAskingForTheSource_ThenItsPathIsPassedOn()
    {
        var state = AppReducer.Reduce(OpenPicker(), new TypeTextCommand("~/work/")).State;

        var update = AppReducer.Reduce(state, new CloneSourceAnswered("https://example.com/team/tool.git"));

        var resolve = Assert.Single(update.Effects.OfType<ResolveCloneParentEffect>());
        Assert.Equal($"{PickerFixtures.PathOf("work")}{Path.DirectorySeparatorChar}", resolve.TypedPath);
        Assert.True(resolve.EndsWithSeparator);
    }

    [Fact]
    public void GivenTheFolder_WhenItIsResolved_ThenTheTargetPromptIsPrefilledWithTheRepositoryName()
    {
        var update = AppReducer.Reduce(
            OpenPicker(),
            new CloneParentResolved("https://example.com/team/tool.git", PickerFixtures.PathOf("code")));

        var request = Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request;
        Assert.Equal("Clone https://example.com/team/tool.git into", request.Context);
        Assert.Equal("clone", request.AcceptLabel);
        Assert.EndsWith("tool", request.InitialText, StringComparison.Ordinal);
        Assert.Contains("code", request.InitialText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenThePickerClosedMeanwhile_WhenTheFolderIsResolved_ThenNoQuestionIsAsked()
    {
        var update = AppReducer.Reduce(Closed(), new CloneParentResolved("source", PickerFixtures.PathOf("code")));

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenATarget_WhenAccepting_ThenTheCloneStartsWithAProgressLine()
    {
        var target = PickerFixtures.PathOf("code", "tool");

        var update = AppReducer.Reduce(OpenPicker(), new CloneTargetAnswered("source", target));

        var clone = Assert.Single(update.Effects.OfType<CloneRepositoryEffect>());
        Assert.Equal("source", clone.Request.Source);
        Assert.Equal(target, clone.Request.TargetDirectory);
        Assert.Equal("Cloning...", update.State.Shell.Creation.RunningClone!.ProgressLine);
    }

    [Fact]
    public void GivenARunningClone_WhenGitReports_ThenTheLineShowsTheStage()
    {
        var state = Cloning();

        state = AppReducer.Reduce(state, new CloneProgressed(new RemoteProgress("Receiving objects", 45))).State;

        Assert.Equal("Cloning: Receiving objects 45%", state.Shell.Creation.RunningClone!.ProgressLine);
    }

    [Fact]
    public void GivenARunningClone_WhenCancelling_ThenTheLineSaysSoAndTheCloneIsStoppedOnce()
    {
        var state = Cloning();

        var update = AppReducer.Reduce(state, new CloneCancelRequested());
        var again = AppReducer.Reduce(update.State, new CloneCancelRequested());

        Assert.Single(update.Effects.OfType<CancelCloneEffect>());
        Assert.EndsWith("(cancelling)", update.State.Shell.Creation.RunningClone!.ProgressLine, StringComparison.Ordinal);
        Assert.Empty(again.Effects);
    }

    [Fact]
    public void GivenNoClone_WhenCancelling_ThenNothingHappens()
    {
        var update = AppReducer.Reduce(OpenPicker(), new CloneCancelRequested());

        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAClone_WhenItSucceeds_ThenTheRepositoryOpensInATabAndTheStatusSaysSo()
    {
        var location = PickerFixtures.Location("code", "tool");

        var update = AppReducer.Reduce(Cloning(), new CloneSucceeded(location));

        Assert.Null(update.State.Shell.Creation.RunningClone);
        Assert.Equal("Cloned tool", Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.Equal(location.RootPath, Assert.Single(update.State.Workspace.Tabs).Location.RootPath);
        Assert.False(update.State.Shell.Picker.IsOpen);
    }

    [Fact]
    public void GivenAClone_WhenItFails_ThenTheErrorShowsAndThePickerStaysOpen()
    {
        var update = AppReducer.Reduce(Cloning(), new CloneFailed("Clone failed: no such source"));

        Assert.Null(update.State.Shell.Creation.RunningClone);
        var status = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Error, status.Severity);
        Assert.True(update.State.Shell.Picker.IsOpen);
        Assert.Empty(update.State.Workspace.Tabs);
    }

    [Fact]
    public void GivenAClone_WhenItIsCancelled_ThenAWarningSaysNothingWasLeftBehind()
    {
        var update = AppReducer.Reduce(Cloning(), new CloneCancelled());

        Assert.Null(update.State.Shell.Creation.RunningClone);
        var status = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(CreationReducer.CancelledText, status.Text);
        Assert.Equal(StatusSeverity.Warning, status.Severity);
    }

    [Fact]
    public void GivenAPathTypedInThePicker_WhenInitialising_ThenThePromptStartsWithIt()
    {
        var state = AppReducer.Reduce(OpenPicker(), new TypeTextCommand("~/code/new")).State;

        var update = AppReducer.Reduce(state, new CreationRequested(CreationKind.Init));

        var request = Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request;
        Assert.Equal("Initialise repository", request.Title);
        Assert.Equal("init", request.AcceptLabel);
        Assert.Equal(OperatingSystem.IsWindows() ? PickerFixtures.PathOf("code", "new") : "~/code/new", request.InitialText);
    }

    [Fact]
    public void GivenNoPathTyped_WhenInitialising_ThenThePromptStartsWithTheBaseDirectory()
    {
        var update = AppReducer.Reduce(OpenPicker(), new CreationRequested(CreationKind.Init));

        var request = Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request;
        Assert.Equal(OperatingSystem.IsWindows() ? PickerFixtures.Home : "~", request.InitialText);
    }

    [Fact]
    public void GivenADirectory_WhenAcceptingTheInit_ThenItRunsThere()
    {
        var update = AppReducer.Reduce(OpenPicker(), new InitDirectoryAnswered("~/code/new"));

        Assert.Equal(PickerFixtures.PathOf("code", "new"), Assert.Single(update.Effects.OfType<InitRepositoryEffect>()).Directory);
        Assert.True(update.State.Shell.Creation.IsInitRunning);
    }

    [Fact]
    public void GivenAnEmptyDirectory_WhenAcceptingTheInit_ThenItAsksAgain()
    {
        var update = AppReducer.Reduce(OpenPicker(), new InitDirectoryAnswered(string.Empty));

        Assert.Equal(CreationReducer.EmptyNotice, Warning(update));
        Assert.IsType<InitDirectoryAnswer>(Assert.Single(update.Effects.OfType<ShowPromptEffect>()).Request.OnAccepted);
    }

    [Theory]
    [InlineData(false, "Initialised made")]
    [InlineData(true, "made already is a repository: opened it")]
    public void GivenAnInit_WhenItSucceeds_ThenTheRepositoryOpensAndTheStatusSaysWhatHappened(bool was, string text)
    {
        var state = OpenPicker();
        state = state with { Shell = state.Shell with { Creation = new CreationSession(null, true) } };

        var update = AppReducer.Reduce(state, new InitSucceeded(PickerFixtures.Location("code", "made"), was));

        Assert.False(update.State.Shell.Creation.IsInitRunning);
        Assert.Equal(text, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Text);
        Assert.Single(update.State.Workspace.Tabs);
    }

    [Fact]
    public void GivenAnInit_WhenItFails_ThenNoTabOpensAndTheErrorShows()
    {
        var state = OpenPicker();
        state = state with { Shell = state.Shell with { Creation = new CreationSession(null, true) } };

        var update = AppReducer.Reduce(state, new InitFailed("Init failed: it is a file"));

        Assert.False(update.State.Shell.Creation.IsInitRunning);
        Assert.Empty(update.State.Workspace.Tabs);
        Assert.Equal(StatusSeverity.Error, Assert.Single(update.Effects.OfType<PostStatusEffect>()).Severity);
    }

    [Fact]
    public void GivenAnOpenCreationPrompt_WhenThePickerCloses_ThenThePromptGoesWithItAndTheKeysAreNormal()
    {
        var state = Run(AppReducer.Reduce(OpenPicker(), new CreationRequested(CreationKind.Clone)));
        Assert.NotNull(state.Shell.Prompt);

        var update = AppReducer.Reduce(state, new PickerCloseRequested());

        Assert.Null(update.State.Shell.Prompt);
        Assert.False(update.State.Shell.Picker.IsOpen);
        Assert.Equal(KeyMode.Normal, Assert.Single(update.Effects.OfType<SetKeyModeEffect>()).Mode);
    }

    [Fact]
    public void GivenAnOpenCreationPrompt_WhenItIsAccepted_ThenTheAnswerCommandIsReducedWithTheTypedText()
    {
        var state = Run(AppReducer.Reduce(OpenPicker(), new CreationRequested(CreationKind.Init)));
        state = AppReducer.Reduce(state, new PromptKeyPressedCommand(new KeyEvent("x"))).State;

        var update = AppReducer.Reduce(state, new PromptAcceptedCommand());

        Assert.Null(update.State.Shell.Prompt);
        Assert.True(update.State.Shell.Creation.IsInitRunning);
        Assert.EndsWith("x", Assert.Single(update.Effects.OfType<InitRepositoryEffect>()).Directory, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenEveryConcreteCreationCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var context = new CreationContext(PickerSession.Empty, string.Empty);
        var commands = ConcreteSubtypes.InstancesOf<CreationCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                CreationReducer.Reduce(CreationSession.Empty, command, context);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in CreationReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static string Warning(AppUpdate update)
    {
        var status = Assert.Single(update.Effects.OfType<PostStatusEffect>());
        Assert.Equal(StatusSeverity.Warning, status.Severity);

        return status.Text;
    }

    private static AppState Closed() => AppState.Empty with
    {
        Shell = ShellState.Empty with { Picker = PickerSession.Empty with { HomeDirectory = PickerFixtures.Home } },
    };

    private static AppState OpenPicker() =>
        AppReducer.Reduce(Closed(), new PickerOpenRequested(null, string.Empty, Start)).State;

    private static AppState Cloning() =>
        AppReducer.Reduce(OpenPicker(), new CloneTargetAnswered("source", PickerFixtures.PathOf("code", "tool"))).State;

    // The runner turns a prompt effect into the prompt command; the tests do the same.
    private static AppState Run(AppUpdate update)
    {
        var state = update.State;
        foreach (var effect in update.Effects.OfType<ShowPromptEffect>())
        {
            state = AppReducer.Reduce(state, new PromptRequestedCommand(effect.Request)).State;
        }

        return state;
    }
}
