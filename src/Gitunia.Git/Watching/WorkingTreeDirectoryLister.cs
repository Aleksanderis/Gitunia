using Gitunia.Git.Execution;

namespace Gitunia.Git.Watching;

/// <summary>Finds the directories to watch with one <c>git ls-files</c> call, so ignored trees (build output, dependencies) cost nothing.</summary>
public sealed class WorkingTreeDirectoryLister : IWorkingTreeDirectoryLister
{
    private static readonly string[] Arguments =
    [
        "-c", "core.quotepath=off", "ls-files", "-z", "--cached", "--others", "--exclude-standard",
    ];

    private static readonly GitRunOptions Options = new(Timeout: TimeSpan.FromSeconds(30), ReadOnly: true);

    private readonly GitRunner _runner;

    public WorkingTreeDirectoryLister(GitRunner runner)
    {
        _runner = runner;
    }

    public async Task<IReadOnlyList<string>?> ListAsync(string workingTreePath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _runner.RunAsync(workingTreePath, Arguments, Options, cancellationToken);

            return WorkingTreeDirectoryParser.Parse(workingTreePath, result.StandardOutput);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }
}
