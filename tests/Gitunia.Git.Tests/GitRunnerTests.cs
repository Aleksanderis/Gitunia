using System.Diagnostics;
using Xunit;
namespace Gitunia.Git.Tests;

public class GitRunnerTests
{
    private static readonly string AnyDirectory = Path.GetTempPath();

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
    public async Task GivenSlowProcess_WhenTimeoutElapses_ThenTimeoutErrorAndProcessKilled()
    {
        var marker = MarkerPath();
        using var fake = FakeGitExecutable.SleepingThenMarking(marker);

        await Assert.ThrowsAsync<GitTimeoutException>(
            () => RunnerFor(fake).RunAsync(
                AnyDirectory,
                ["fetch"],
                new GitRunOptions(Timeout: TimeSpan.FromMilliseconds(300)),
                TestContext.Current.CancellationToken));

        await AssertNeverMarkedAsync(marker);
    }

    [Fact]
    public async Task GivenSlowProcess_WhenCancelled_ThenOperationCancelledAndProcessKilled()
    {
        var marker = MarkerPath();
        using var fake = FakeGitExecutable.SleepingThenMarking(marker);
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => RunnerFor(fake).RunAsync(AnyDirectory, ["fetch"], cancellationToken: source.Token));

        await AssertNeverMarkedAsync(marker);
    }

    [Fact]
    public async Task GivenMissingExecutable_WhenRunning_ThenNotFoundError()
    {
        var runner = new GitRunner(new GitRunnerOptions(Path.Combine(AnyDirectory, "gitunia-no-such-git-" + Guid.NewGuid().ToString("N"))));

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

        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(collected.ToArray()));
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
                new GitRunOptions(Timeout: TimeSpan.FromMilliseconds(500)),
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
            new GitRunOptions(Timeout: TimeSpan.FromMilliseconds(200)),
            TestContext.Current.CancellationToken))
        {
            // Intrinsic wait: the consumer must be slower than the runner's timeout for the test to mean anything.
            await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);
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
        var runner = new GitRunner(new GitRunnerOptions(fake.Path, EnvironmentOverrides: new Dictionary<string, string?> { ["GITUNIA_PROBE"] = "yes" }));

        var result = await runner.RunAsync(AnyDirectory, ["status"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("0|yes", result.StandardOutputText);
    }

    private static GitRunner RunnerFor(FakeGitExecutable fake) => new(new GitRunnerOptions(fake.Path));

    private static string MarkerPath() => Path.Combine(Path.GetTempPath(), "gitunia-marker-" + Guid.NewGuid().ToString("N"));

    private static async Task AssertNeverMarkedAsync(string marker)
    {
        // The fake would write the marker after two seconds had the process survived; wait for its exit rather than for the clock.
        if (OperatingSystem.IsWindows())
        {
            // The cmd script cannot record its process id, so the only option is to outwait it.
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
        else if (!File.Exists(marker + ".pid"))
        {
            Assert.Fail("the fake never recorded its process id");
        }
        else if (int.TryParse((await File.ReadAllTextAsync(marker + ".pid")).Trim(), out var processId))
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
