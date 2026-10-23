using System.Diagnostics;
using System.Text;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class GitRunnerTests
{
    private static readonly string AnyDirectory = Path.GetTempPath();

    // The runner's timer also covers process start-up, which can exceed a few hundred ms under CPU load; the fake must have recorded its id before the kill.
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan CancelDelayWithoutProcessId = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SlowConsumerTimeout = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan SlowConsumerDelay = TimeSpan.FromMilliseconds(1500);

    // Only bounds a hang.
    private static readonly TimeSpan SignalGuardTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task GivenRealGit_WhenAskingVersion_ThenOutputIsCaptured()
    {
        var runner = new GitRunner();

        var result = await runner.RunGlobalAsync(["--version"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version", result.StandardOutputText);
    }

    [Fact]
    public async Task GivenFailingProcess_WhenRunning_ThenTypedErrorCarriesExitCodeAndStderr()
    {
        using var fake = FakeGitExecutable.Failing(3, "boom");

        var error = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => RunnerFor(fake).RunAsync(AnyDirectory, ["status"], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(3, error.ExitCode);
        Assert.Contains("boom", error.StandardError);
    }

    [Fact]
    public async Task GivenAcceptedExitCode_WhenRunning_ThenNoError()
    {
        using var fake = FakeGitExecutable.Failing(1, "differences");

        var result = await RunnerFor(fake).RunAsync(
            AnyDirectory,
            ["diff"],
            new GitRunOptions(AcceptedExitCodes: [1]),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task GivenCarriageReturnProgress_WhenRunning_ThenEverySegmentIsReportedAndStderrIsKept()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake prints with sh.");
        using var fake = FakeGitExecutable.Create("printf 'a 1%%\\ra 2%%\\nb' >&2", "rem");
        var segments = new List<string>();

        var result = await RunnerFor(fake).RunAsync(
            AnyDirectory,
            ["fetch"],
            new GitRunOptions(OnStandardErrorLine: segments.Add),
            TestContext.Current.CancellationToken);

        Assert.Equal(["a 1%", "a 2%", "b"], segments);
        Assert.Equal("a 1%\ra 2%\nb", result.StandardError);
    }

    [Fact]
    public async Task GivenStderrKeepsArriving_WhenRunningLongerThanTheTimeout_ThenItIsNotTimedOut()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake prints with sh.");
        using var fake = FakeGitExecutable.Create(
            "i=0; while [ $i -lt 8 ]; do echo x >&2; sleep 0.2; i=$((i+1)); done",
            "rem");

        var result = await RunnerFor(fake).RunAsync(
            AnyDirectory,
            ["fetch"],
            new GitRunOptions(Timeout: TimeSpan.FromSeconds(1), OnStandardErrorLine: _ => { }),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task GivenStderrGoesQuiet_WhenTheIdleLimitElapses_ThenTimeoutErrorAndProcessKilled()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake prints with sh.");
        using var fake = FakeGitExecutable.Create("echo x >&2\nsleep 30", "rem");

        await Assert.ThrowsAsync<GitTimeoutException>(
            () => RunnerFor(fake).RunAsync(
                AnyDirectory,
                ["fetch"],
                new GitRunOptions(Timeout: KillTimeout, OnStandardErrorLine: _ => { }),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenSlowProcess_WhenTimeoutElapses_ThenTimeoutErrorAndProcessKilled()
    {
        var marker = MarkerPath();
        using var fake = FakeGitExecutable.SleepingThenMarking(marker);

        await Assert.ThrowsAsync<GitTimeoutException>(
            () => RunnerFor(fake).RunAsync(
                AnyDirectory,
                ["fetch"],
                new GitRunOptions(Timeout: OperatingSystem.IsWindows() ? CancelDelayWithoutProcessId : KillTimeout),
                TestContext.Current.CancellationToken));

        await AssertNeverMarkedAsync(marker);
    }

    [Fact]
    public async Task GivenSlowProcess_WhenCancelled_ThenOperationCancelledAndProcessKilled()
    {
        var marker = MarkerPath();
        using var fake = FakeGitExecutable.SleepingThenMarking(marker);
        using var recorded = ProcessIdSignal.Watch(marker);
        using var source = new CancellationTokenSource();
        var run = RunnerFor(fake).RunAsync(AnyDirectory, ["fetch"], cancellationToken: source.Token);

        if (OperatingSystem.IsWindows())
        {
            // The cmd script cannot record its process id, so the cancellation is timed instead.
            source.CancelAfter(CancelDelayWithoutProcessId);
        }
        else
        {
            await recorded.Recorded.WaitAsync(SignalGuardTimeout, TestContext.Current.CancellationToken);
            await source.CancelAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        await AssertNeverMarkedAsync(marker);
    }

    [Fact]
    public async Task GivenMissingExecutable_WhenRunning_ThenNotFoundError()
    {
        var runner = new GitRunner(new GitRunnerOptions(Path.Combine(AnyDirectory, $"gitunia-no-such-git-{Guid.NewGuid().ToString("N")}")));

        await Assert.ThrowsAsync<GitNotFoundException>(
            () => runner.RunAsync(AnyDirectory, ["status"], cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenOutputProcess_WhenStreaming_ThenChunksConcatenateToOutput()
    {
        using var fake = FakeGitExecutable.Printing("hello");
        var collected = new List<byte>();

        await foreach (var chunk in RunnerFor(fake).StreamAsync(AnyDirectory, ["log"], cancellationToken: TestContext.Current.CancellationToken))
        {
            collected.AddRange(chunk);
        }

        Assert.Equal("hello", Encoding.UTF8.GetString([.. collected]));
    }

    [Fact]
    public async Task GivenFailingProcess_WhenStreaming_ThenErrorSurfacesAfterEnumeration()
    {
        using var fake = FakeGitExecutable.Failing(2, "bad");

        await Assert.ThrowsAsync<GitCommandFailedException>(async () =>
        {
            await foreach (var unused in RunnerFor(fake).StreamAsync(AnyDirectory, ["log"], cancellationToken: TestContext.Current.CancellationToken))
            {
                Assert.NotNull(unused);
            }
        });
    }

    // The idle timer also covers process start-up before the first chunk, so the timeout keeps a margin
    // over a loaded machine's start time.
    [Fact]
    public async Task GivenStalledStream_WhenIdleTimeoutElapses_ThenTimeoutAfterFirstChunk()
    {
        using var fake = FakeGitExecutable.PrintingThenSleeping("first");
        var chunks = 0;

        await Assert.ThrowsAsync<GitTimeoutException>(async () =>
        {
            await foreach (var unused in RunnerFor(fake).StreamAsync(
                AnyDirectory,
                ["log"],
                new GitRunOptions(Timeout: TimeSpan.FromSeconds(1)),
                TestContext.Current.CancellationToken))
            {
                chunks += unused.Length > 0 ? 1 : 0;
            }
        });

        Assert.Equal(1, chunks);
    }

    [Fact]
    public async Task GivenStream_WhenConsumerStopsEarly_ThenProcessIsKilled()
    {
        var marker = MarkerPath();
        using var fake = FakeGitExecutable.SleepingThenMarking(marker);

        await foreach (var unused in RunnerFor(fake).StreamAsync(AnyDirectory, ["log"], cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.NotEmpty(unused);
            break;
        }

        await AssertNeverMarkedAsync(marker);
    }

    [Fact]
    public async Task GivenSlowConsumer_WhenHandlingChunkLongerThanTimeout_ThenNoTimeout()
    {
        using var fake = FakeGitExecutable.Printing("data");
        var collected = 0;

        await foreach (var chunk in RunnerFor(fake).StreamAsync(
            AnyDirectory,
            ["log"],
            new GitRunOptions(Timeout: SlowConsumerTimeout),
            TestContext.Current.CancellationToken))
        {
            // Intrinsic wait: the consumer must be slower than the runner's timeout for the test to mean anything.
            // The timeout also covers the wait for the child's first output, so it stays well above a loaded
            // machine's start time (#634).
            await Task.Delay(SlowConsumerDelay, TestContext.Current.CancellationToken);
            collected += chunk.Length;
        }

        Assert.Equal(4, collected);
    }

    [Fact]
    public async Task GivenRepositoryPathAndReadOnly_WhenRunning_ThenGitReceivesExplicitPathQuotepathAndNoOptionalLocks()
    {
        using var fake = FakeGitExecutable.EchoingArgumentsAndLocks();

        var result = await RunnerFor(fake).RunAsync(
            AnyDirectory,
            ["status"],
            new GitRunOptions(ReadOnly: true),
            TestContext.Current.CancellationToken);

        var text = result.StandardOutputText;
        Assert.Contains("-C", text);
        Assert.Contains(AnyDirectory, text);
        Assert.Contains("core.quotepath=off", text);
        Assert.Contains("status", text);
        Assert.Contains("LOCKS=0", text);
    }

    [Fact]
    public async Task GivenNotReadOnly_WhenRunning_ThenOptionalLocksUnset()
    {
        using var fake = FakeGitExecutable.EchoingArgumentsAndLocks();

        var result = await RunnerFor(fake).RunAsync(AnyDirectory, ["status"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("LOCKS=", result.StandardOutputText);
        Assert.DoesNotContain("LOCKS=0", result.StandardOutputText);
    }

    [Fact]
    public async Task GivenEnvironmentOverride_WhenRunning_ThenProcessSeesIt()
    {
        using var fake = FakeGitExecutable.Create("printf '%s|%s' \"$GIT_TERMINAL_PROMPT\" \"$GITUNIA_PROBE\"", "<nul set /p =\"%GIT_TERMINAL_PROMPT%|%GITUNIA_PROBE%\"");
        var runner = new GitRunner(new GitRunnerOptions(fake.ExecutablePath, EnvironmentOverrides: new Dictionary<string, string?> { ["GITUNIA_PROBE"] = "yes" }));

        var result = await runner.RunAsync(AnyDirectory, ["status"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("0|yes", result.StandardOutputText);
    }

    [Fact]
    public async Task GivenDefaultOptions_WhenCommandExitsWithOne_ThenItThrows()
    {
        using var testRepository = TestRepositories.Linear(1);
        var runner = new GitRunner();

        await Assert.ThrowsAsync<GitCommandFailedException>(() => runner.RunAsync(testRepository.WorkingTreePath, ["config", "--get", "no.such.key"], null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenAcceptedExitCode_WhenCommandExitsWithOne_ThenItReturnsTheResult()
    {
        using var testRepository = TestRepositories.Linear(1);
        var runner = new GitRunner();

        var result = await runner.RunAsync(testRepository.WorkingTreePath, ["config", "--get", "no.such.key"], GitRunOptions.ReadOnlyRunAllowingNoMatch, TestContext.Current.CancellationToken);

        Assert.Equal(GitRunOptions.NoMatchExitCode, result.ExitCode);
    }

    private static GitRunner RunnerFor(FakeGitExecutable fake) => new(new GitRunnerOptions(fake.ExecutablePath));

    private static string MarkerPath() => Path.Combine(Path.GetTempPath(), $"gitunia-marker-{Guid.NewGuid().ToString("N")}");

    private static async Task AssertNeverMarkedAsync(string marker)
    {
        // The fake would write the marker after its sleep (30 seconds on Unix) had the process survived; wait for its exit rather than for the clock.
        if (OperatingSystem.IsWindows())
        {
            // The cmd script cannot record its process id, so the only option is to outwait it.
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        else if (!File.Exists($"{marker}.pid"))
        {
            Assert.Fail("the fake never recorded its process id");
        }
        else if (int.TryParse((await File.ReadAllTextAsync($"{marker}.pid")).Trim(), out var processId))
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                Assert.True(process.WaitForExit(TimeSpan.FromSeconds(30)), "the process was not killed");
            }
            catch (ArgumentException)
            {
                // Already gone.
            }
        }

        Assert.False(File.Exists(marker), "the process was not killed");
    }
}
