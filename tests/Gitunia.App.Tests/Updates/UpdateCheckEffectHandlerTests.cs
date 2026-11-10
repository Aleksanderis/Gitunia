using Gitunia.App.Background;
using Gitunia.App.Updates;
using Gitunia.Core;
using Gitunia.Core.Updates;
using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

public sealed class UpdateCheckEffectHandlerTests : IDisposable
{
    private readonly string _stateDirectory = Path.Combine(Path.GetTempPath(), $"gitunia-update-handler-{Guid.NewGuid():N}");
    private readonly FakeReleaseServer _server = new();
    private readonly RecordingDispatcher _dispatcher = new();
    private readonly ShutdownTasks _tasks = new();
    private readonly BackgroundWork _background = new();

    public void Dispose()
    {
        _background.Dispose();
        if (Directory.Exists(_stateDirectory))
        {
            Directory.Delete(_stateDirectory, recursive: true);
        }
    }

    [Fact]
    public void GivenANewerRelease_WhenHandlingTheEffect_ThenOneCompletedCommandCarriesTheNotice()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));

        Handler(Checker()).Handle(CancellationToken.None);
        _tasks.Wait(TimeSpan.FromSeconds(10));

        var completed = Assert.IsType<UpdateCheckCompleted>(Assert.Single(_dispatcher.Posted));
        Assert.Equal("0.9.0", completed.Notice!.Version);
    }

    [Fact]
    public void GivenNoChecker_WhenHandlingTheEffect_ThenTheResultIsNoNotice()
    {
        Handler(null).Handle(CancellationToken.None);
        _tasks.Wait(TimeSpan.FromSeconds(10));

        Assert.Null(Assert.IsType<UpdateCheckCompleted>(Assert.Single(_dispatcher.Posted)).Notice);
    }

    [Fact]
    public void GivenAnEndpointThatKnowsNothing_WhenHandlingTheEffect_ThenTheResultIsNoNoticeAndNothingThrows()
    {
        Handler(Checker()).Handle(CancellationToken.None);
        _tasks.Wait(TimeSpan.FromSeconds(10));

        Assert.Null(Assert.IsType<UpdateCheckCompleted>(Assert.Single(_dispatcher.Posted)).Notice);
    }

    [Fact]
    public void GivenACancelledToken_WhenHandlingTheEffect_ThenNothingIsPosted()
    {
        _server.Serve(FakeRelease.LatestUrl, FakeRelease.Json("0.9.0", []));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Handler(Checker()).Handle(cancellation.Token);
        _tasks.Wait(TimeSpan.FromSeconds(10));

        Assert.Empty(_dispatcher.Posted);
    }

    private UpdateCheckEffectHandler Handler(UpdateChecker? checker) =>
        new(checker, _background, _dispatcher, _tasks);

    private UpdateChecker Checker() => new(
        new UpdateCheckCache(_stateDirectory),
        "0.8.0",
        new Uri(FakeRelease.LatestUrl),
        new InstallFactsReader(Path.Combine(_stateDirectory, "bin", "gitunia")),
        TimeProvider.System,
        _server);

    private sealed class RecordingDispatcher : IDispatcher
    {
        private readonly List<AppCommand> _posted = [];

        public IReadOnlyList<AppCommand> Posted
        {
            get
            {
                lock (_posted)
                {
                    return [.. _posted];
                }
            }
        }

        public void Post(AppCommand command)
        {
            lock (_posted)
            {
                _posted.Add(command);
            }
        }
    }
}
