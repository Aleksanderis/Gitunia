using Gitunia.App.Ignore;
using Gitunia.App.Staging;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Ignore;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Ignore;
using Gitunia.Git.Repositories.Models;
using Xunit;

namespace Gitunia.App.Tests.Ignore;

// The handler is the only place that writes .gitignore: a fake writer shows what it is asked, the order shows when the
// tab is freed.
public class IgnoreEffectHandlerTests
{
    private static readonly TabId Tab = new(1);
    private static readonly RepositoryLocation Nowhere = new("/r", "/r/.git", "/r/.git", false, false);

    [Fact]
    public async Task GivenAPattern_WhenAdding_ThenItIsWrittenAndTheResultFollowsTheStatusRead()
    {
        var run = new Run();

        var result = await run.ExecuteAsync(new AddIgnorePatternEffect(Tab, Nowhere, "*.log"));

        var success = Assert.IsType<IgnoreSucceeded>(result);
        Assert.Equal("Added *.log to .gitignore", success.Text);
        Assert.Equal(StatusSeverity.Info, success.Severity);
        Assert.Equal(["*.log"], run.Writer.Added);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenThePatternWasAlreadyThere_WhenAdding_ThenTheResultIsAWarningThatSaysSo()
    {
        var run = new Run();
        run.Writer.Outcome = IgnoreAddOutcome.AlreadyListed;

        var result = await run.ExecuteAsync(new AddIgnorePatternEffect(Tab, Nowhere, "*.log"));

        var success = Assert.IsType<IgnoreSucceeded>(result);
        Assert.StartsWith("*.log is already in .gitignore", success.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
    }

    [Fact]
    public async Task GivenTheFileCannotBeWritten_WhenAdding_ThenTheFailureNamesTheReasonAndTheStatusIsStillRead()
    {
        var run = new Run();
        run.Writer.Failure = new IOException("read-only file system");

        var result = await run.ExecuteAsync(new AddIgnorePatternEffect(Tab, Nowhere, "*.log"));

        var failed = Assert.IsType<IgnoreFailed>(result);
        Assert.Equal("Could not update .gitignore: read-only file system", failed.Text);
        Assert.Equal(["refresh", "result"], run.Events);
    }

    [Fact]
    public async Task GivenTheTabIsClosed_WhenAdding_ThenTheCancelIsPostedAtOnceWithoutAStatusRead()
    {
        var run = new Run();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await run.ExecuteWithTokenAsync(new AddIgnorePatternEffect(Tab, Nowhere, "*.log"), cancelled.Token);

        Assert.IsType<IgnoreCancelled>(result);
        Assert.Equal(["result"], run.Events);
    }

    private sealed class FakeIgnoreWriter : IIgnoreWriter
    {
        public List<string> Added { get; } = [];

        public IgnoreAddOutcome Outcome { get; set; } = IgnoreAddOutcome.Added;

        public Exception? Failure { get; set; }

        public Task<IgnoreAddOutcome> AddAsync(
            RepositoryLocation location,
            string pattern,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null)
            {
                throw Failure;
            }

            Added.Add(pattern);

            return Task.FromResult(Outcome);
        }
    }

    // The handler over a fake writer, a refresher and a dispatcher that record the order in which the status is read and
    // the result is posted.
    private sealed class Run : IStatusRefresher
    {
        private readonly RecordingDispatcher _dispatcher = new();
        private readonly IgnoreEffectHandler _handler;

        public Run()
        {
            _handler = new IgnoreEffectHandler(Writer, this, new Ordered(this));
        }

        public FakeIgnoreWriter Writer { get; } = new();

        public List<string> Events { get; } = [];

        public void RefreshThen(TabId tabId, Action done)
        {
            Events.Add("refresh");
            done();
        }

        public Task<AppCommand> ExecuteAsync(IgnoreEffect effect) =>
            ExecuteWithTokenAsync(effect, TestContext.Current.CancellationToken);

        public async Task<AppCommand> ExecuteWithTokenAsync(IgnoreEffect effect, CancellationToken cancellationToken)
        {
            _handler.Handle(effect, cancellationToken);

            return await _dispatcher.NextAsync(TestContext.Current.CancellationToken);
        }

        private sealed class Ordered(Run run) : IDispatcher
        {
            public void Post(AppCommand command)
            {
                run.Events.Add("result");
                run._dispatcher.Post(command);
            }
        }
    }
}
