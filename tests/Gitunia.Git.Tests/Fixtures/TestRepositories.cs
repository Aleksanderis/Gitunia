namespace Gitunia.Git.Tests.Fixtures;

/// <summary>Ready-made repository shapes for tests; each call builds a fresh repository.</summary>
public static class TestRepositories
{
    /// <summary>A single branch <c>main</c> with <paramref name="commitCount"/> commits.</summary>
    public static TestRepository Linear(int commitCount = 3)
    {
        TestRepository repository = TestRepository.Create();
        for (int index = 1; index <= commitCount; index++)
        {
            repository.Commit($"Commit {index}", ("file.txt", $"line {index}\n"));
        }

        return repository;
    }

    /// <summary>main and feature diverge and are merged; branch <c>topic</c> points at the first parent of the merge and tag <c>v1</c> at the merge commit. Deliberately minimal: octopus merges, annotated tags, detached HEAD, locked/prunable/dirty worktrees, renames and binary files are added by the parser issues that need them.</summary>
    public static TestRepository Branchy()
    {
        TestRepository repository = TestRepository.Create()
            .Commit("Initial", ("base.txt", "base\n"))
            .CheckoutNewBranch("feature")
            .Commit("Feature one", ("feature.txt", "one\n"))
            .Commit("Feature two", ("feature.txt", "two\n"))
            .Checkout("main")
            .Commit("Main work", ("main.txt", "main\n"))
            .Merge("feature")
            .Branch("topic", "HEAD~1")
            .Tag("v1");
        return repository;
    }

    /// <summary>main plus a linked worktree <c>wt</c> on branch <c>wt-branch</c>.</summary>
    public static TestRepository WithWorktree(out string worktreePath)
    {
        TestRepository repository = Linear(2);
        worktreePath = repository.AddWorktree("wt", "wt-branch", createBranch: true);
        return repository;
    }

    /// <summary>A merge of <c>other</c> into main stopped on a conflict in <c>conflict.txt</c>.</summary>
    public static TestRepository Conflicted()
    {
        return TestRepository.Create()
            .Commit("Initial", ("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", ("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("Main change", ("conflict.txt", "main\n"))
            .MergeExpectingConflict("other");
    }

    /// <summary>Paths with spaces, unicode, quotes and nested directories.</summary>
    public static TestRepository OddPaths()
    {
        return TestRepository.Create().Commit(
            "Odd paths",
            ("with space.txt", "a\n"),
            ("ünï cødé.txt", "b\n"),
            ("quote's.txt", "c\n"),
            (Path.Combine("dir with space", "nested file.txt"), "d\n"));
    }
}
