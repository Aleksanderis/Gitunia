using System.Text;
using Gitunia.Git.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Tests.Staging;
using Xunit;

namespace Gitunia.Git.Tests.Discard;

/// <summary>Real git readers and small builders shared by the discard tests; every repository is a throwaway temp repository.</summary>
public static class DiscardSupport
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StatusReader Status = new(Runner);

    public static GitRunner SharedRunner => Runner;

    public static DiffReader DiffReaderInstance { get; } = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A file of <paramref name="count"/> lines <c>l1</c>, <c>l2</c> ... where some lines have other text.</summary>
    public static string Lines(int count, params LineEdit[] replaced)
    {
        var builder = new StringBuilder();
        for (var line = 1; line <= count; line++)
        {
            var edit = replaced.FirstOrDefault(change => change.Line == line);
            builder.Append(edit?.Text ?? $"l{line}").Append('\n');
        }

        return builder.ToString();
    }

    public static LineEdit Edit(int line, string text) => new(line, text);

    public static async Task<LocatedEntry> LocateAsync(TestRepository repository, string path)
    {
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var snapshot = await Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        return new LocatedEntry(location, snapshot.Entries.Single(entry => entry.Path == path));
    }

    public static Task<RepositoryLocation> LocationOfAsync(TestRepository repository) =>
        Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    public static string Read(TestRepository repository, string path) =>
        File.ReadAllText(Path.Combine(repository.WorkingTreePath, path));

    /// <summary>The changed lines of the unstaged diff of a hunk (or those with these texts), as a user would pick them.</summary>
    public static async Task<IReadOnlyList<ChangedLine>> ChangesAsync(
        LocatedEntry located,
        int? hunk = null,
        string[]? texts = null)
    {
        DiffFile? file = null;
        var changes = DiffReaderInstance.ReadWorkingFileAsync(
            located.Location,
            WorkingDiffSide.Unstaged,
            located.Entry,
            cancellationToken: Token);
        await foreach (var candidate in changes)
        {
            file = candidate;
        }

        Assert.NotNull(file);
        var picked = new List<ChangedLine>();
        for (var hunkIndex = 0; hunkIndex < file.Hunks.Count; hunkIndex++)
        {
            foreach (var line in file.Hunks[hunkIndex].Lines.Where(line => line.Kind != DiffLineKind.Context))
            {
                var isPicked = hunk is { } wanted ? wanted == hunkIndex : texts!.Contains(line.Text);
                if (isPicked)
                {
                    var number = line.Kind == DiffLineKind.Removed ? line.OldLineNumber : line.NewLineNumber;
                    picked.Add(new ChangedLine(line.Kind, number!.Value, line.Text));
                }
            }
        }

        return picked;
    }
}
