namespace Gitunia.Git.Tests.Fixtures;

/// <summary>Ready-made repository shapes for tests; each call builds a fresh repository.</summary>
public static class TestRepositories
{
    /// <summary>A single branch <c>main</c> with <paramref name="commitCount"/> commits.</summary>
    public static TestRepository Linear(int commitCount = 3)
    {
        var repository = TestRepository.Create();
        for (var index = 1; index <= commitCount; index++)
        {
            repository.Commit($"Commit {index}", new TestFile("file.txt", $"line {index}\n"));
        }

        return repository;
    }

    /// <summary>Main and feature diverge and are merged; branch <c>topic</c> points at the first parent of the merge and tag <c>v1</c> at the merge commit. Deliberately minimal: octopus merges, annotated tags, detached HEAD, locked/prunable/dirty worktrees, renames and binary files are added by the parser issues that need them.</summary>
    public static TestRepository WithMergedFeatureBranch()
    {
        var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("feature")
            .Commit("Feature one", new TestFile("feature.txt", "one\n"))
            .Commit("Feature two", new TestFile("feature.txt", "two\n"))
            .Checkout("main")
            .Commit("Main work", new TestFile("main.txt", "main\n"))
            .Merge("feature")
            .Branch("topic", "HEAD~1")
            .Tag("v1");
        return repository;
    }

    /// <summary>Main plus a linked worktree <c>wt</c> on branch <c>wt-branch</c>.</summary>
    public static TestRepository WithWorktree(out string worktreePath)
    {
        var repository = Linear(2);
        worktreePath = repository.AddWorktree("wt", "wt-branch", createBranch: true);
        return repository;
    }

    /// <summary>A merge of <c>other</c> into main stopped on a conflict in <c>conflict.txt</c>.</summary>
    public static TestRepository Conflicted()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", "main\n"))
            .MergeExpectingConflict("other");
    }

    /// <summary>Linear main pushed to a local bare origin with upstream tracking set.</summary>
    public static TestRepository WithOrigin() => Linear(2).AddOrigin();

    /// <summary>Branch <c>topic</c> tracking <c>origin/topic</c> with <paramref name="ahead"/> local and <paramref name="behind"/> remote-only commits.</summary>
    public static TestRepository WithTrackingBranch(int ahead, int behind)
    {
        var repository = WithOrigin().CheckoutNewBranch("topic").Commit("Topic base", new TestFile("topic.txt", "base\n"));
        repository.PushWithUpstream("topic");
        for (var index = 1; index <= behind; index++)
        {
            repository.CommitOnOrigin("topic", $"Remote {index}", new TestFile($"remote{index}.txt", "r\n"));
        }

        for (var index = 1; index <= ahead; index++)
        {
            repository.Commit($"Local {index}", new TestFile($"local{index}.txt", "l\n"));
        }

        return repository;
    }

    /// <summary>Branch <c>lonely</c> that exists only locally, with no upstream.</summary>
    public static TestRepository WithoutUpstreamBranch() =>
        WithOrigin().CheckoutNewBranch("lonely").Commit("Lonely", new TestFile("lonely.txt", "x\n"));

    /// <summary>Main plus linked worktrees <c>wt-locked</c> (locked), <c>wt-prunable</c> (directory deleted) and <c>wt-dirty</c> (uncommitted change).</summary>
    public static TestRepository WithManyWorktrees(out string lockedPath, out string prunablePath, out string dirtyPath)
    {
        var repository = Linear(2);
        lockedPath = repository.AddWorktree("wt-locked", "locked", createBranch: true);
        prunablePath = repository.AddWorktree("wt-prunable", "prunable", createBranch: true);
        dirtyPath = repository.AddWorktree("wt-dirty", "dirty", createBranch: true);
        repository.Git("worktree", "lock", "--reason", "test lock", lockedPath);
        Directory.Delete(prunablePath, recursive: true);
        File.WriteAllText(Path.Combine(dirtyPath, "file.txt"), "dirty\n");
        return repository;
    }

    /// <summary>Branch <c>other</c> and main edit <c>conflict.txt</c> differently; a rebase of <c>other</c> onto main is stopped on the conflict.</summary>
    public static TestRepository RebaseConflicted() => ConflictingBranches().Checkout("other").RebaseExpectingConflict("main");

    /// <summary>A cherry-pick of <c>other</c> onto main stopped on a conflict in <c>conflict.txt</c>.</summary>
    public static TestRepository CherryPickConflicted() => ConflictingBranches().CherryPickExpectingConflict("other");

    /// <summary>A revert of the first change to <c>conflict.txt</c> stopped on a conflict (a later commit changed the same line).</summary>
    public static TestRepository RevertConflicted()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .Commit("First change", new TestFile("conflict.txt", "first\n"))
            .Commit("Second change", new TestFile("conflict.txt", "second\n"))
            .RevertExpectingConflict("HEAD~1");
    }

    /// <summary>Linear repository with one stash holding a tracked and an untracked change.</summary>
    public static TestRepository WithStash() => Linear(2).StashWithUntracked();

    private static TestRepository ConflictingBranches()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", "main\n"));
    }

    /// <summary>Paths with spaces, unicode, quotes and nested directories.</summary>
    public static TestRepository OddPaths()
    {
        return TestRepository.Create().Commit(
            "Odd paths",
            new TestFile("with space.txt", "a\n"),
            new TestFile("ünï cødé.txt", "b\n"),
            new TestFile("quote's.txt", "c\n"),
            new TestFile(Path.Combine("dir with space", "nested file.txt"), "d\n"));
    }
}
