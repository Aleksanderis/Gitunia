using System.Diagnostics;
using System.Text;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Execution;

public class GitRunnerInputTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StatusReader Status = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenStandardInput_WhenRunning_ThenGitReadsItAndOutputIsCaptured()
    {
        using var testRepository = TestRepositories.Linear(1);

        var result = await Runner.RunAsync(
            testRepository.WorkingTreePath,
            ["hash-object", "--stdin"],
            new GitRunOptions(StandardInput: Encoding.UTF8.GetBytes("hello\n")),
            Token);

        Assert.Equal("ce013625030ba8dba906f756967f9e9ca394464a", result.StandardOutputText.Trim());
    }

    [Fact]
    public async Task GivenNoStandardInput_WhenGitReadsStdin_ThenItSeesEndOfInputAtOnce()
    {
        using var testRepository = TestRepositories.Linear(1);

        var result = await Runner.RunAsync(testRepository.WorkingTreePath, ["hash-object", "--stdin"], cancellationToken: Token);

        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", result.StandardOutputText.Trim());
    }

    [Fact]
    public async Task GivenLargeStandardInput_WhenRunning_ThenAllBytesArrive()
    {
        using var testRepository = TestRepositories.Linear(1);
        var input = new byte[3 * 1024 * 1024];
        Array.Fill(input, (byte)'x');

        var result = await Runner.RunAsync(
            testRepository.WorkingTreePath,
            ["hash-object", "--stdin"],
            new GitRunOptions(StandardInput: input),
            Token);
        File.WriteAllBytes(Path.Combine(testRepository.WorkingTreePath, "big.bin"), input);
        var fromFile = await Runner.RunAsync(testRepository.WorkingTreePath, ["hash-object", "big.bin"], cancellationToken: Token);

        Assert.Equal(fromFile.StandardOutputText, result.StandardOutputText);
    }

    [Fact]
    public async Task GivenGitExitsWithoutReadingLargeInput_WhenRunning_ThenCommandFailureIsReportedNotAnIoError()
    {
        using var testRepository = TestRepositories.Linear(1);
        var input = new byte[8 * 1024 * 1024];

        var error = await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Runner.RunAsync(
                testRepository.WorkingTreePath,
                ["rev-parse", "--verify", "no-such-ref"],
                new GitRunOptions(StandardInput: input),
                Token));

        Assert.NotEqual(0, error.ExitCode);
    }

    [Fact]
    public async Task GivenGitNeverReadsLargeInput_WhenTimeoutElapses_ThenTimeoutErrorInsteadOfHanging()
    {
        using var fake = FakeGitExecutable.Sleeping();
        var runner = new GitRunner(new GitRunnerOptions(fake.ExecutablePath));
        var input = new byte[8 * 1024 * 1024];

        await Assert.ThrowsAsync<GitTimeoutException>(
            () => runner.RunAsync(
                Path.GetTempPath(),
                ["fetch"],
                new GitRunOptions(Timeout: TimeSpan.FromMilliseconds(500), StandardInput: input),
                Token));
    }

    [Fact]
    public async Task GivenStandardInputOnAStream_WhenStreaming_ThenItIsIgnoredAndStdinIsClosed()
    {
        using var testRepository = TestRepositories.Linear(1);
        var output = new List<byte>();

        await foreach (var chunk in Runner.StreamAsync(
            testRepository.WorkingTreePath,
            ["hash-object", "--stdin"],
            new GitRunOptions(StandardInput: Encoding.UTF8.GetBytes("hello\n")),
            Token))
        {
            output.AddRange(chunk.Span);
        }

        Assert.Equal("e69de29bb2d1d6434b8b29ae775ad8c2e48c5391", Encoding.UTF8.GetString([.. output]).Trim());
    }

    [Fact]
    public async Task GivenPartialPatch_WhenApplyingFromStandardInputToTheIndex_ThenOnlyTheSelectedLineIsStaged()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Commit("base", new TestFile("a.txt", "1\n2\n3\n4\n5\n"));
        testRepository.WriteFile("a.txt", "1\n2 changed\n3\n4\n5 changed\n");
        var diff = await Runner.RunAsync(testRepository.WorkingTreePath, ["diff", "--no-color", "-U1"], cancellationToken: Token);
        var file = DiffParser.Parse(diff.StandardOutputText).Items.Single();
        var hunk = file.Hunks[0];
        var firstAdded = hunk.Lines.ToList().FindIndex(line => line.Kind == DiffLineKind.Added);
        var selection = new Dictionary<int, IReadOnlyCollection<int>> { [0] = [firstAdded - 1, firstAdded] };

        var patch = DiffPatchWriter.WriteSelection(file, selection, PatchDirection.Forward);
        await Runner.RunAsync(
            testRepository.WorkingTreePath,
            ["apply", "--cached", "-"],
            new GitRunOptions(StandardInput: Encoding.UTF8.GetBytes(patch)),
            Token);

        var staged = await Runner.RunAsync(testRepository.WorkingTreePath, ["diff", "--cached", "--no-color"], cancellationToken: Token);
        Assert.Contains("+2 changed", staged.StandardOutputText);
        Assert.DoesNotContain("5 changed", staged.StandardOutputText);
    }

    [Fact]
    public async Task GivenNonUtf8FileName_WhenReadingStatusAndStagingByBytes_ThenNameRoundTripsAndFileIsStaged()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Only Linux allows file names that are not UTF-8.");
        using var testRepository = TestRepositories.Linear(1);
        var name = new byte[] { 0x61, 0xFF, 0xFE, 0x2E, 0x74, 0x78, 0x74 };
        CreateFileNamedByBytes(testRepository.WorkingTreePath, name);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var before = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);

        var entry = Assert.Single(before.Entries);
        Assert.Equal(name, GitPathText.Encode(entry.Path));
        await Runner.RunAsync(
            testRepository.WorkingTreePath,
            ["add", GitOptions.PathspecFromStandardInput, GitOptions.PathspecFileNul],
            new GitRunOptions(StandardInput: GitPathText.EncodePathspecFile([entry.Path])),
            Token);
        var after = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        var staged = Assert.Single(after.Entries);
        Assert.Equal(FileChangeKind.Added, staged.IndexChange);
        Assert.Equal(entry.Path, staged.Path);
    }

    private static void CreateFileNamedByBytes(string directory, byte[] name)
    {
        // The runtime cannot spell a non-UTF-8 name, so the shell creates the file from an octal escape.
        var octal = string.Concat(name.Select(value => $"\\{Convert.ToString(value, 8).PadLeft(3, '0')}"));
        var startInfo = new ProcessStartInfo("sh") { WorkingDirectory = directory };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"printf x > \"$(printf '{octal}')\"");
        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
