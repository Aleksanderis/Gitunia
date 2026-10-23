using Gitunia.Git.Rebase;
using Xunit;

namespace Gitunia.Git.Tests.Rebase;

public class RebaseTodoTests
{
    private static readonly RebaseTodoCommit[] Range =
    [
        new("aaa", "First"),
        new("bbb", "Second"),
        new("ccc", "Third"),
    ];

    [Fact]
    public void GivenEveryAction_WhenFormatting_ThenEachGetsItsCommandAndARewordGetsAnAmendLine()
    {
        RebaseTodoEntry[] entries =
        [
            new("aaa", RebaseTodoAction.Pick),
            new("bbb", RebaseTodoAction.Reword, "New"),
            new("ccc", RebaseTodoAction.Squash),
            new("ddd", RebaseTodoAction.Fixup),
            new("eee", RebaseTodoAction.Drop),
        ];

        var text = RebaseTodoFormatter.Format(entries, [null, "/g/reword-1.txt", null, null, null]);

        Assert.Equal(
            "pick aaa\npick bbb\nexec git commit --amend --allow-empty -F '/g/reword-1.txt'\n" +
            "squash ccc\nfixup ddd\ndrop eee\n",
            text);
    }

    [Fact]
    public void GivenAnApostropheInAPath_WhenQuoting_ThenTheShellSeesOneWord()
    {
        Assert.Equal("'/it'\\''s here/x'", RebaseTodoFormatter.ShellQuote("/it's here/x"));
    }

    [Fact]
    public void GivenAValidReorder_WhenValidating_ThenNothingIsThrown()
    {
        var exception = Record.Exception(() => RebaseTodoValidator.Validate(
            Range,
            [
                new("ccc", RebaseTodoAction.Pick),
                new("aaa", RebaseTodoAction.Drop),
                new("bbb", RebaseTodoAction.Squash),
            ]));

        Assert.Null(exception);
    }

    [Fact]
    public void GivenACommitLeftOut_WhenValidating_ThenItIsRefusedBecauseOmissionWouldDropItSilently()
    {
        var exception = Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(
            Range,
            [new("aaa", RebaseTodoAction.Pick), new("bbb", RebaseTodoAction.Pick)]));

        Assert.Contains("2 of the 3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenADuplicate_WhenValidating_ThenItIsRefused()
    {
        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(
            Range,
            [new("aaa", RebaseTodoAction.Pick), new("aaa", RebaseTodoAction.Pick), new("bbb", RebaseTodoAction.Pick)]));
    }

    [Fact]
    public void GivenAnAbbreviatedOrForeignHash_WhenValidating_ThenItIsRefused()
    {
        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(
            Range,
            [new("aa", RebaseTodoAction.Pick), new("bbb", RebaseTodoAction.Pick), new("ccc", RebaseTodoAction.Pick)]));
    }

    [Theory]
    [InlineData(RebaseTodoAction.Squash)]
    [InlineData(RebaseTodoAction.Fixup)]
    public void GivenAMeldAfterOnlyDrops_WhenValidating_ThenItIsRefused(RebaseTodoAction action)
    {
        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(
            Range,
            [new("aaa", RebaseTodoAction.Drop), new("bbb", action), new("ccc", RebaseTodoAction.Pick)]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n")]
    public void GivenARewordWithoutText_WhenValidating_ThenItIsRefused(string? message)
    {
        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(
            Range,
            [
                new("aaa", RebaseTodoAction.Reword, message),
                new("bbb", RebaseTodoAction.Pick),
                new("ccc", RebaseTodoAction.Pick),
            ]));
    }

    [Fact]
    public void GivenStructureAndMergeEntries_WhenFormatting_ThenTheyKeepGitsWords()
    {
        RebaseTodoEntry[] entries =
        [
            new("onto", RebaseTodoAction.Label),
            new("onto", RebaseTodoAction.Reset),
            new("aaa", RebaseTodoAction.Pick),
            new("mmm", RebaseTodoAction.Merge, Argument: "side # Merge side"),
        ];

        var text = RebaseTodoFormatter.Format(entries, [null, null, null, null]);

        Assert.Equal("label onto\nreset onto\npick aaa\nmerge -C mmm side # Merge side\n", text);
    }

    [Fact]
    public void GivenGitsRebaseMergesTodo_WhenParsing_ThenEveryLineKindIsRead()
    {
        const string todo = "label onto\n\n# Branch side\nreset onto\npick aaa # A\nlabel side\n\n" +
            "reset [new root]\nreset side # c2\npick bbb # B\nmerge -C mmm side # Merge side\n\n# Rebase x..y\n#\n";

        var lines = RebaseTodoParser.Parse(todo);

        Assert.Equal(
            [
                (RebaseTodoAction.Label, "onto"),
                (RebaseTodoAction.Reset, "onto"),
                (RebaseTodoAction.Pick, "aaa"),
                (RebaseTodoAction.Label, "side"),
                (RebaseTodoAction.Reset, "[new root]"),
                (RebaseTodoAction.Reset, "side"),
                (RebaseTodoAction.Pick, "bbb"),
                (RebaseTodoAction.Merge, "mmm"),
            ],
            lines.Select(line => (line.Action, line.Hash)));
        Assert.Equal("A", lines[2].Subject);
        Assert.Equal("Merge side", lines[^1].Subject);
        Assert.Equal("side # Merge side", lines[^1].Argument);
    }

    [Fact]
    public void GivenACommandTheEditorCannotCarry_WhenParsing_ThenItIsRefusedInsteadOfDropped()
    {
        Assert.Throws<RebaseRangeException>(() => RebaseTodoParser.Parse("pick aaa # A\nexec make\n"));
    }

    private static readonly RebaseTodoCommit[] MergeRange =
    [
        new("onto", string.Empty, RebaseTodoAction.Label),
        new("onto", string.Empty, RebaseTodoAction.Reset),
        new("aaa", "A"),
        new("side", string.Empty, RebaseTodoAction.Label),
        new("onto", string.Empty, RebaseTodoAction.Reset),
        new("bbb", "B"),
        new("mmm", "Merge", RebaseTodoAction.Merge, "side # Merge"),
    ];

    [Fact]
    public void GivenTheRangeUnchanged_WhenValidatingAMergeRange_ThenNothingIsThrown()
    {
        var exception = Record.Exception(() => RebaseTodoValidator.Validate(MergeRange, MergeEntries()));

        Assert.Null(exception);
    }

    [Fact]
    public void GivenALabelLeftOut_WhenValidating_ThenItIsRefused()
    {
        var entries = MergeEntries().Where(entry => entry.Commit != "side").ToList();

        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(MergeRange, entries));
    }

    [Fact]
    public void GivenTheMergeDropped_WhenValidating_ThenItIsRefused()
    {
        var entries = MergeEntries();
        entries[^1] = new RebaseTodoEntry("mmm", RebaseTodoAction.Drop);

        Assert.Throws<RebaseRangeException>(() => RebaseTodoValidator.Validate(MergeRange, entries));
    }

    [Fact]
    public void GivenTheMergeMovedBeforeItsLabel_WhenFindingProblems_ThenTheLabelOrderIsReported()
    {
        var entries = MergeEntries();
        var merge = entries[^1];
        entries.RemoveAt(entries.Count - 1);
        entries.Insert(2, merge);

        Assert.Contains("side", RebaseTodoValidator.FindProblem(entries), StringComparison.Ordinal);
        Assert.Null(RebaseTodoValidator.FindProblem(MergeEntries()));
    }

    [Fact]
    public void GivenASquashRightAfterAReset_WhenFindingProblems_ThenItIsReported()
    {
        var entries = MergeEntries();
        entries[5] = new RebaseTodoEntry("bbb", RebaseTodoAction.Squash);

        Assert.Contains("reset", RebaseTodoValidator.FindProblem(entries), StringComparison.Ordinal);
    }

    private static List<RebaseTodoEntry> MergeEntries() =>
        [.. MergeRange.Select(line => new RebaseTodoEntry(line.Hash, line.Action, Argument: line.Argument))];

    [Fact]
    public void GivenGitsTodoPath_WhenTheEditorModeRuns_ThenItIsOverwrittenWithThePreparedTodo()
    {
        var directory = Directory.CreateTempSubdirectory("gitunia-sequence-editor");
        try
        {
            var prepared = Path.Combine(directory.FullName, "prepared");
            var gits = Path.Combine(directory.FullName, "git-rebase-todo");
            File.WriteAllText(prepared, "drop aaa\n");
            File.WriteAllText(gits, "pick aaa\n");

            var exitCode = SequenceEditor.Copy([prepared, gits]);

            Assert.Equal(0, exitCode);
            Assert.Equal("drop aaa\n", File.ReadAllText(gits));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GivenAMissingPreparedTodo_WhenTheEditorModeRuns_ThenItFailsWithANonZeroExitCode()
    {
        var directory = Directory.CreateTempSubdirectory("gitunia-sequence-editor");
        try
        {
            var missing = Path.Combine(directory.FullName, "missing");
            var exitCode = SequenceEditor.Copy([missing, Path.Combine(directory.FullName, "todo")]);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void GivenSpacesAndApostrophes_WhenBuildingTheConfigValue_ThenEveryWordIsQuotedForTheShell()
    {
        var command = new SequenceEditorCommand(["/opt/my apps/gitunia", "--sequence-editor"]);

        Assert.Equal(
            "'/opt/my apps/gitunia' '--sequence-editor' '/tmp/it'\\''s/todo'",
            command.ToConfigValue("/tmp/it's/todo"));
    }

    [Fact]
    public void GivenTheWrongNumberOfArguments_WhenTheEditorModeRuns_ThenItFailsWithUsageError()
    {
        Assert.Equal(2, SequenceEditor.Copy(["only-one"]));
    }
}
