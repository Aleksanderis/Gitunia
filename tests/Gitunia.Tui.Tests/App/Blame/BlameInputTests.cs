using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.Core.Blame;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Blame;

// The blame view through the real path: the input class, the reducer, the effect handler and real git.
public sealed class BlameInputTests
{
    private const int Width = 100;
    private const int Height = 30;

    [Fact]
    public async Task GivenTwoCommits_WhenOpeningTheBlame_ThenTheGutterShowsIdAuthorAgeAndTheLine()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));

        await harness.OpenAsync();

        var frame = harness.Input.CurrentFrame()!;
        var first = frame.Lines.Single(line => line.Text.Contains("one"));
        var second = frame.Lines.Single(line => line.Text.Contains("TWO"));
        Assert.Equal(Height - 1, frame.Height);
        Assert.Contains("Blame: a.txt @", frame.Lines[0].Text);
        Assert.Contains($"^{repository.Git("rev-parse", "--short=7", "HEAD~1").Trim()}", first.Text);
        Assert.Contains(repository.Head[..7], second.Text);
        Assert.Contains("ago", second.Text);
        Assert.Contains("|", second.Text);
        Assert.Contains("1/3", frame.Lines[^2].Text);
    }

    [Fact]
    public async Task GivenAnEditedWorkingTreeFile_WhenOpeningTheBlame_ThenTheNewLineShowsAsUncommitted()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "one\nTWO\nthree\nextra\n");
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", null));

        await harness.OpenAsync();

        var frame = harness.Input.CurrentFrame()!;
        Assert.Contains("uncommitted", frame.Lines.Single(line => line.Text.Contains("extra")).Text);
        Assert.Contains("working tree", frame.Lines[0].Text);
    }

    [Fact]
    public async Task GivenALine_WhenPressingEnter_ThenTheViewClosesAndTheGraphIsAskedForThatLinesCommit()
    {
        using var repository = TwoCommits();
        var first = repository.Git("rev-parse", "HEAD~1").Trim();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        Assert.True(harness.Input.TryExecute(ActionIds.BlameShowCommit, null));
        var locate = Assert.Single(harness.Effects.OfType<LocateGraphCommitEffect>());

        Assert.False(harness.Input.IsOpen);
        Assert.Equal(first, locate.Commit.ToString());
        Assert.Null(harness.Input.CurrentFrame());
    }

    [Fact]
    public async Task GivenALineOfTheSecondCommit_WhenMovingDownAndPressingEnter_ThenThatCommitIsLocated()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.ListDown, null);
        harness.Input.TryExecute(ActionIds.BlameShowCommit, null);

        Assert.Equal(repository.Head, Assert.Single(harness.Effects.OfType<LocateGraphCommitEffect>()).Commit.ToString());
    }

    [Fact]
    public async Task GivenTypedSearchText_WhenAccepting_ThenTheCursorLandsOnTheMatchingLine()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Input.TryExecute(ActionIds.SearchOpen, null);
        Assert.True(harness.Input.HandleTextKey(new KeyEvent("t")));
        Assert.True(harness.Input.HandleTextKey(new KeyEvent("h")));
        harness.Input.TryExecute(ActionIds.InputAccept, null);
        await harness.PumpUntilAsync(() => harness.View.Cursor == 2);

        Assert.Equal(KeyMode.Normal, harness.Host.State.Shell.Mode);
        Assert.Contains("/th", harness.Input.CurrentFrame()!.Lines[^2].Text);
    }

    [Fact]
    public async Task GivenNothingSelected_WhenOpeningTheBlame_ThenTheStatusSaysSoAndNoViewOpens()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets(null, null));

        Assert.True(harness.Input.TryExecute(ActionIds.BlameOpen, null));

        Assert.False(harness.Input.IsOpen);
        Assert.Equal("nothing selected", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenTheView_WhenClickingALineAndTheActionRow_ThenTheLineIsHighlightedAndTheRowNamesItsMouseView()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        var onLine = harness.Input.OnLineClicked(3, out var lineHandled);
        var onAction = harness.Input.OnLineClicked(Height - 1 - 4 + 1, out var actionHandled);

        Assert.Null(onLine);
        Assert.True(lineHandled);
        Assert.Equal(2, harness.View.Cursor);
        Assert.Equal(DefaultMouseMap.BlameShowButton, onAction);
        Assert.True(actionHandled);
        Assert.Equal(ActionIds.BlameShowCommit, DefaultMouseMap.Create().Resolve(onAction!, MouseGesture.Click)!.Action);
    }

    [Fact]
    public async Task GivenNarrowAndTinyScreens_WhenDrawing_ThenTheGutterShrinksAndATinyScreenDrawsNothing()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        harness.Resize(40, 12);
        var narrow = harness.Input.CurrentFrame()!;
        harness.Resize(10, 5);
        var tiny = harness.Input.CurrentFrame();

        Assert.All(narrow.Lines, line => Assert.Equal(40, line.Text.Length));
        Assert.DoesNotContain("ago", narrow.Lines[2].Text);
        Assert.Null(tiny);
    }

    [Fact]
    public async Task GivenTheView_WhenAnActionNobodyOwnsIsPressed_ThenTheViewConsumesItAndStaysOpen()
    {
        using var repository = TwoCommits();
        using var harness = await Harness.LoadAsync(repository, new FixedTargets("a.txt", repository.Head));
        await harness.OpenAsync();

        Assert.True(harness.Input.TryExecute(ActionIds.BranchDelete, null));

        Assert.True(harness.Input.IsOpen);
    }

    private static TestRepository TwoCommits() => TestRepository.Create()
        .Commit("first", new TestFile("a.txt", "one\nthree\n"))
        .Commit("second", new TestFile("a.txt", "one\nTWO\nthree\n"));

    private sealed class FixedTargets(string? path, string? revision) : IBlameTargets
    {
        public BlameTarget Resolve(RepositoryState tab) =>
            path is null ? BlameTarget.Refused("nothing selected") : BlameTarget.Of(path, revision);
    }

    private sealed class Harness : IDisposable
    {
        private readonly UiPump _pump = new(TestContext.Current.CancellationToken);
        private readonly Dispatcher _host;
        private readonly BlameEffectHandler _handler;

        private Harness(IBlameTargets targets)
        {
            _host = new Dispatcher(AppState.Empty, _pump);
            Store = new BlameStore();
            _handler = new BlameEffectHandler(TestGit.Services.Blame, Store, _host);
            Input = new BlameInput(_host, Store, targets, GraphGlyphSet.Unicode);
            _host.EffectRaised += Effects.Add;
            _host.EffectRaised += Route;
            Statuses = new StatusReporter(_host);
            Resize(Width, Height);
        }

        public Dispatcher Host => _host;

        public BlameStore Store { get; }

        public BlameInput Input { get; }

        public List<AppEffect> Effects { get; } = [];

        public StatusReporter Statuses { get; }

        public BlameState View => _host.State.Shell.Blame.View!;

        public static async Task<Harness> LoadAsync(TestRepository repository, IBlameTargets targets)
        {
            var harness = new Harness(targets);
            var location = await TestGit.Services.Discovery.DiscoverAsync(
                repository.WorkingTreePath,
                TestContext.Current.CancellationToken);
            harness._host.Dispatch(new OpenRepositoryCommand(location));

            return harness;
        }

        public void Resize(int width, int height) =>
            _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(width, height), _host.Clock.GetUtcNow()));

        public async Task OpenAsync()
        {
            Assert.True(Input.TryExecute(ActionIds.BlameOpen, null));
            await PumpUntilAsync(() => _host.State.Shell.Blame.View is { IsComplete: true });
        }

        public Task PumpUntilAsync(Func<bool> condition) => _pump.RunUntilAsync(condition);

        public void Dispose()
        {
        }

        private void Route(AppEffect effect)
        {
            switch (effect)
            {
                case StartBlameEffect start:
                    _handler.Start(start, TestContext.Current.CancellationToken);
                    break;
                case StopBlameEffect stop:
                    _handler.Stop(stop);
                    break;
                case FindBlameMatchEffect find:
                    _handler.Find(find);
                    break;
                case SetKeyModeEffect mode:
                    _host.Dispatch(new SetKeyModeCommand(mode.Mode));
                    break;
                case PostStatusEffect post:
                    _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
                    break;
            }
        }
    }
}
