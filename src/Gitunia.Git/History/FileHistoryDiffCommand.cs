using Gitunia.Git.History.Models;

namespace Gitunia.Git.History;

/// <summary>
/// The <c>IDiffReader.ReadAsync</c> command that prints one commit's diff of one history entry's file.
/// </summary>
public static class FileHistoryDiffCommand
{
    private const string Show = "show";
    private const string NoFormat = "--format=";
    private const string SplitMerges = "-m";
    private const string FirstParent = "--first-parent";

    /// <summary>
    /// <c>git show</c> of the commit limited to the file: against the first parent for a merge, and with both names of
    /// a rename in the pathspec so git pairs them instead of showing a deletion and an addition.
    /// </summary>
    /// <param name="entry">The history entry.</param>
    public static IReadOnlyList<string> For(FileHistoryEntry entry)
    {
        var paths = new List<string> { $"{GitOptions.LiteralPathspecPrefix}{entry.Path}" };
        if (entry.OldPath is { } oldPath)
        {
            paths.Add($"{GitOptions.LiteralPathspecPrefix}{oldPath}");
        }

        return
        [
            Show,
            NoFormat,
            SplitMerges,
            FirstParent,
            entry.Hash,
            GitOptions.EndOfOptions,
            .. paths,
        ];
    }
}
