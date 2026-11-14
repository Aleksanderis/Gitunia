namespace Gitunia.Git.Tests.Fixtures;

/// <summary>
/// Ready-made repository shapes for tests; each call returns a fresh, independent repository. A shape is built with git
/// once per test process and every call gets a copy of it (<see cref="TestRepository.FromTemplate"/>), because a git
/// process costs far more than copying files on macOS and Windows (#1388).
/// </summary>
public static class TestRepositories
{
    private const string WorktreeName = "wt";

    /// <summary>A single branch <c>main</c> with <paramref name="commitCount"/> commits.</summary>
    public static TestRepository Linear(int commitCount = 3) =>
        TestRepository.FromTemplate($"linear-{commitCount}", () => BuildLinear(commitCount));

    /// <summary>
    /// Main and feature diverge and are merged; branch <c>topic</c> points at the first parent of the merge and tag
    /// <c>v1</c> at the merge commit. Deliberately minimal: octopus merges, annotated tags, detached HEAD, renames and
    /// binary files are added by the parser issues that need them.
    /// </summary>
    public static TestRepository WithMergedFeatureBranch() =>
        TestRepository.FromTemplate("merged-feature-branch", BuildMergedFeatureBranch);

    /// <summary>Main plus a linked worktree <c>wt</c> on branch <c>wt-branch</c>.</summary>
    public static TestRepository WithWorktree(out string worktreePath)
    {
        var repository = TestRepository.FromTemplate("worktree", BuildWithWorktree);
        worktreePath = repository.WorktreePath(WorktreeName);
        return repository;
    }

    /// <summary>A merge of <c>other</c> into main stopped on a conflict in <c>conflict.txt</c>.</summary>
    public static TestRepository Conflicted() => TestRepository.FromTemplate("conflicted", BuildConflicted);

    /// <summary>
    /// A merge stopped on <c>conflict.txt</c>, a 72-line file with five conflict hunks (lines 5, 20, 36, 52 and 66)
    /// spread between unconflicted lines.
    /// </summary>
    public static TestRepository ConflictedManyHunks() =>
        TestRepository.FromTemplate("conflicted-many-hunks", BuildConflictedManyHunks);

    /// <summary>Linear main pushed to a local bare origin with upstream tracking set.</summary>
    public static TestRepository WithOrigin() => TestRepository.FromTemplate("origin", BuildWithOrigin);

    /// <summary>
    /// Branch <c>topic</c> tracking <c>origin/topic</c> with <paramref name="ahead"/> local and
    /// <paramref name="behind"/> remote-only commits.
    /// </summary>
    public static TestRepository WithTrackingBranch(int ahead, int behind) =>
        TestRepository.FromTemplate($"tracking-{ahead}-{behind}", () => BuildWithTrackingBranch(ahead, behind));

    /// <summary>Branch <c>lonely</c> that exists only locally, with no upstream.</summary>
    public static TestRepository WithoutUpstreamBranch() =>
        TestRepository.FromTemplate("without-upstream", BuildWithoutUpstreamBranch);

    /// <summary>
    /// Main plus linked worktrees <c>wt-locked</c> (locked), <c>wt-prunable</c> (directory deleted) and
    /// <c>wt-dirty</c> (uncommitted change).
    /// </summary>
    public static TestRepository WithManyWorktrees(
        out string lockedPath,
        out string prunablePath,
        out string dirtyPath)
    {
        var repository = TestRepository.FromTemplate("many-worktrees", BuildWithManyWorktrees);
        lockedPath = repository.WorktreePath("wt-locked");
        prunablePath = repository.WorktreePath("wt-prunable");
        dirtyPath = repository.WorktreePath("wt-dirty");
        return repository;
    }

    /// <summary>
    /// Branch <c>other</c> and main edit <c>conflict.txt</c> differently; a rebase of <c>other</c> onto main is
    /// stopped on the conflict.
    /// </summary>
    public static TestRepository RebaseConflicted() =>
        TestRepository.FromTemplate("rebase-conflicted", BuildRebaseConflicted);

    /// <summary>A cherry-pick of <c>other</c> onto main stopped on a conflict in <c>conflict.txt</c>.</summary>
    public static TestRepository CherryPickConflicted() =>
        TestRepository.FromTemplate("cherry-pick-conflicted", BuildCherryPickConflicted);

    /// <summary>
    /// A revert of the first change to <c>conflict.txt</c> stopped on a conflict (a later commit changed the line).
    /// </summary>
    public static TestRepository RevertConflicted() =>
        TestRepository.FromTemplate("revert-conflicted", BuildRevertConflicted);

    /// <summary>Linear repository with one stash holding a tracked and an untracked change.</summary>
    public static TestRepository WithStash() => TestRepository.FromTemplate("stash", BuildWithStash);

    /// <summary>Main and other both changed conflict.txt after their fork; main is checked out.</summary>
    public static TestRepository ConflictingBranches() =>
        TestRepository.FromTemplate("conflicting-branches", BuildConflictingBranches);

    /// <summary>Paths with spaces, unicode, quotes and nested directories.</summary>
    public static TestRepository OddPaths() => TestRepository.FromTemplate("odd-paths", BuildOddPaths);

    private static TestRepository BuildLinear(int commitCount)
    {
        var repository = TestRepository.Create();
        for (var index = 1; index <= commitCount; index++)
        {
            repository.Commit($"Commit {index}", new TestFile("file.txt", $"line {index}\n"));
        }

        return repository;
    }

    private static TestRepository BuildMergedFeatureBranch()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("base.txt", "base\n"))
            .CheckoutNewBranch("feature")
            .Commit("Feature one", new TestFile("feature.txt", "one\n"))
            .Commit("Feature two", new TestFile("feature.txt", "two\n"))
            .Checkout("main")
            .Commit("Main work", new TestFile("main.txt", "main\n"))
            .Merge("feature")
            .Branch("topic", "HEAD~1")
            .Tag("v1");
    }

    private static TestRepository BuildWithWorktree()
    {
        var repository = BuildLinear(2);
        repository.AddWorktree(WorktreeName, "wt-branch", createBranch: true);
        return repository;
    }

    private static TestRepository BuildConflicted()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", "main\n"))
            .MergeExpectingConflict("other");
    }

    private static TestRepository BuildConflictedManyHunks()
    {
        int[] changed = [5, 20, 36, 52, 66];
        var lines = Enumerable.Range(1, 72).Select(number => $"line {number}").ToArray();
        string Variant(string side) => string.Join(
            '\n',
            lines.Select((line, index) => Array.IndexOf(changed, index + 1) >= 0 ? $"{line} ({side})" : line)) + "\n";

        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", string.Join('\n', lines) + "\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile("conflict.txt", Variant("other")))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", Variant("main")))
            .MergeExpectingConflict("other");
    }

    private static TestRepository BuildWithOrigin() => BuildLinear(2).AddOrigin();

    private static TestRepository BuildWithTrackingBranch(int ahead, int behind)
    {
        var repository = BuildWithOrigin()
            .CheckoutNewBranch("topic")
            .Commit("Topic base", new TestFile("topic.txt", "base\n"));
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

    private static TestRepository BuildWithoutUpstreamBranch() =>
        BuildWithOrigin().CheckoutNewBranch("lonely").Commit("Lonely", new TestFile("lonely.txt", "x\n"));

    private static TestRepository BuildWithManyWorktrees()
    {
        var repository = BuildLinear(2);
        var lockedPath = repository.AddWorktree("wt-locked", "locked", createBranch: true);
        var prunablePath = repository.AddWorktree("wt-prunable", "prunable", createBranch: true);
        var dirtyPath = repository.AddWorktree("wt-dirty", "dirty", createBranch: true);
        repository.Git("worktree", "lock", "--reason", "test lock", lockedPath);
        Directory.Delete(prunablePath, recursive: true);
        File.WriteAllText(Path.Combine(dirtyPath, "file.txt"), "dirty\n");
        return repository;
    }

    private static TestRepository BuildRebaseConflicted() =>
        BuildConflictingBranches().Checkout("other").RebaseExpectingConflict("main");

    private static TestRepository BuildCherryPickConflicted() =>
        BuildConflictingBranches().CherryPickExpectingConflict("other");

    private static TestRepository BuildRevertConflicted()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .Commit("First change", new TestFile("conflict.txt", "first\n"))
            .Commit("Second change", new TestFile("conflict.txt", "second\n"))
            .RevertExpectingConflict("HEAD~1");
    }

    private static TestRepository BuildWithStash() => BuildLinear(2).StashWithUntracked();

    private static TestRepository BuildConflictingBranches()
    {
        return TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other change", new TestFile("conflict.txt", "other\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", "main\n"));
    }

    private static TestRepository BuildOddPaths()
    {
        return TestRepository.Create().Commit(
            "Odd paths",
            new TestFile("with space.txt", "a\n"),
            new TestFile("ünï cødé.txt", "b\n"),
            new TestFile("quote's.txt", "c\n"),
            new TestFile(Path.Combine("dir with space", "nested file.txt"), "d\n"));
    }
}
