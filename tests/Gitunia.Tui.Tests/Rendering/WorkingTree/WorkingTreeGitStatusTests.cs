using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.WorkingTree;

/// <summary>The acceptance of #29: the two lists show what <c>git status</c> reports, on repositories with renames and conflicts.</summary>
public class WorkingTreeGitStatusTests
{
    private const int ListWidth = 80;
    private const string UntrackedCode = "??";
    private const string RenameArrow = " -> ";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenRenamesDeletionsAndUntrackedFiles_WhenRenderingTheLists_ThenTheyMatchGitStatus()
    {
        using var repository = TestRepository.Create()
            .Commit("root", new TestFile("keep.txt", "keep\n"), new TestFile("old.txt", "one\ntwo\nthree\nfour\n"), new TestFile("gone.txt", "bye\n"), new TestFile("both.txt", "a\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.WriteFile("new.txt", "one\ntwo\nthree\nfour\nfive\n");
        repository.WriteFile("keep.txt", "keep\nmore\n");
        repository.Delete("gone.txt");
        repository.WriteFile("fresh.txt", "fresh\n");
        repository.Git("add", "fresh.txt");
        repository.WriteFile("both.txt", "a\nb\n");
        repository.Git("add", "both.txt");
        repository.WriteFile("both.txt", "a\nb\nc\n");
        repository.WriteFile("dir/untracked.txt", "x\n");

        await AssertListsMatchGitStatusAsync(repository);
    }

    [Fact]
    public async Task GivenAMergeConflict_WhenRenderingTheLists_ThenTheConflictComesFirstAndMatchesGitStatus()
    {
        using var repository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "a\n"), new TestFile("conflict.txt", "base\n"))
            .CheckoutNewBranch("other")
            .Commit("other", new TestFile("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("main", new TestFile("conflict.txt", "main\n"));
        repository.MergeExpectingConflict("other");
        repository.WriteFile("a.txt", "a\nchanged\n");

        var rows = await AssertListsMatchGitStatusAsync(repository);

        Assert.Equal("! conflict.txt", rows.Unstaged[0]);
        Assert.Contains("both modified", rows.FirstUnstagedRow, StringComparison.Ordinal);
    }

    private static async Task<ListRows> AssertListsMatchGitStatusAsync(TestRepository repository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var snapshot = await TestGit.Services.Status.ReadStatusAsync(location, false, UntrackedFilesMode.All, Token);
        var tree = WorkingTreeState.Reconcile(null, snapshot);

        var unstaged = Rows(tree, WorkingTreeSection.Unstaged);
        var staged = Rows(tree, WorkingTreeSection.Staged);

        var expected = ExpectedFromGit(repository.Git("status", "--porcelain=v1", "--untracked-files=all"));
        Assert.Equal(expected.Unstaged.Order(StringComparer.Ordinal), unstaged.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Staged.Order(StringComparer.Ordinal), staged.Order(StringComparer.Ordinal));
        Assert.Equal(expected.Unstaged.Count, WorkingTreePaneRenderer.Count(tree, WorkingTreeSection.Unstaged));

        var first = WorkingTreePaneRenderer.RenderRow(tree, WorkingTreeSection.Unstaged, 0, ListWidth, false, GraphGlyphSet.Unicode).Text;
        return new ListRows(unstaged, staged) { FirstUnstagedRow = first };
    }

    private static List<string> Rows(WorkingTreeState tree, WorkingTreeSection section)
    {
        var count = WorkingTreePaneRenderer.Count(tree, section);
        return [.. Enumerable.Range(0, count).Select(index => TextOf(tree, section, index))];
    }

    // The row without its padding and the conflict note, as "L path".
    private static string TextOf(WorkingTreeState tree, WorkingTreeSection section, int index)
    {
        var text = WorkingTreePaneRenderer.RenderRow(tree, section, index, ListWidth, false, GraphGlyphSet.Unicode).Text.TrimEnd();
        var entries = section == WorkingTreeSection.Unstaged ? tree.Unstaged : tree.Staged;
        return entries[index].Kind == StatusEntryKind.Unmerged ? text[..text.IndexOf("  ", 2, StringComparison.Ordinal)] : text;
    }

    // Git's own two-letter codes decide which list a path belongs to and which letter it shows there.
    private static ListRows ExpectedFromGit(string porcelain)
    {
        var unstaged = new List<string>();
        var staged = new List<string>();
        foreach (var line in porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var code = line[..2];
            var path = line[3..];
            if (code == UntrackedCode)
            {
                unstaged.Add($"? {path}");
            }
            else if (code.Contains('U', StringComparison.Ordinal) || code is "AA" or "DD")
            {
                unstaged.Add($"! {path}");
            }
            else
            {
                if (code[0] != ' ')
                {
                    staged.Add($"{code[0]} {path}");
                }

                if (code[1] != ' ')
                {
                    // Porcelain v1 prints "old -> new" for a rename; the unstaged change is of the new path only.
                    var arrow = path.IndexOf(RenameArrow, StringComparison.Ordinal);
                    unstaged.Add($"{code[1]} {(arrow >= 0 ? path[(arrow + RenameArrow.Length)..] : path)}");
                }
            }
        }

        return new ListRows(unstaged, staged);
    }

    private sealed record ListRows(List<string> Unstaged, List<string> Staged)
    {
        public string FirstUnstagedRow { get; init; } = string.Empty;
    }
}
