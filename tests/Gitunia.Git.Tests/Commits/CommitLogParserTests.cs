using System.Text;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitLogParserTests
{
    [Fact]
    public void GivenMergeAndRootCommits_WhenParsing_ThenParentsAreOrdered()
    {
        var text = Record(3, [2, 1]) + Record(2, [1]) + Record(1, []);

        var commits = ParseAll(text, 1000, out _);

        Assert.Equal(3, commits.Count);
        Assert.Equal([HashFor(2), HashFor(1)], commits[0].Parents.Select(parent => parent.ToString()));
        Assert.Empty(commits[2].Parents);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), commits[0].AuthorTime);
    }

    [Theory]
    [InlineData("1700000000 -0500", -300)]
    [InlineData("1700000000 +0530", 330)]
    [InlineData("1700000000 +0000", 0)]
    [InlineData("1700000000", 0)]
    [InlineData("1700000000 bogus", 0)]
    [InlineData("1700000000 +-105", 0)]
    [InlineData("253402300799 +0500", 0)]
    [InlineData("-62135596800 -0500", 0)]
    public void GivenRawAuthorDate_WhenParsing_ThenTheAuthorOffsetIsKept(string raw, int offsetMinutes)
    {
        var text = string.Join('\x1f', HashFor(1), string.Empty, "A", "a@b", raw, string.Empty, "Subject") + "\0";

        var commits = ParseAll(text, 1000, out _);

        Assert.Equal(TimeSpan.FromMinutes(offsetMinutes), commits[0].AuthorTime.Offset);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(long.Parse(raw.Split(' ')[0])), commits[0].AuthorTime);
    }

    [Fact]
    public void GivenSha256Hashes_WhenParsing_ThenFullLengthIsKept()
    {
        var hash = new string('a', 64);
        var text = string.Join('\x1f', hash, hash.Replace('a', 'b'), "A", "a@b", "1", string.Empty, "Subject") + "\0";

        var commit = Assert.Single(ParseAll(text, 1000, out _));

        Assert.Equal(hash, commit.Id.ToString());
        Assert.Equal(hash.Replace('a', 'b'), Assert.Single(commit.Parents).ToString());
    }

    [Fact]
    public void GivenAnyChunking_WhenParsing_ThenResultIsIdentical()
    {
        var text = Record(2, [1], author: "Zoë 日本", subject: "ünï subject with spaces") + Record(1, [], decorations: "HEAD -> refs/heads/main, tag: refs/tags/v1");
        var whole = ParseAll(text, 100_000, out _);

        for (var chunkSize = 1; chunkSize < 40; chunkSize++)
        {
            var chunked = ParseAll(text, chunkSize, out var parser);

            Assert.Equal(whole.Count, chunked.Count);
            for (var index = 0; index < whole.Count; index++)
            {
                Assert.Equal(whole[index].Id, chunked[index].Id);
                Assert.Equal(whole[index].AuthorName, chunked[index].AuthorName);
                Assert.Equal(whole[index].Subject, chunked[index].Subject);
                Assert.Equal(whole[index].Decorations, chunked[index].Decorations);
            }

            Assert.Empty(parser.Warnings);
        }
    }

    [Fact]
    public void GivenDecorations_WhenParsing_ThenKindsAndCurrentBranchAreSet()
    {
        var decorations = "HEAD -> refs/heads/main, tag: refs/tags/v1, refs/remotes/origin/main, refs/heads/feature/x, refs/stash";
        var commits = ParseAll(Record(1, [], decorations: decorations), 1000, out _);

        var parsed = commits[0].Decorations;

        Assert.Equal(new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true), parsed[0]);
        Assert.Equal(new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false), parsed[1]);
        Assert.Equal(new CommitDecoration("refs/remotes/origin/main", "origin/main", RefKind.RemoteBranch, false), parsed[2]);
        Assert.Equal("feature/x", parsed[3].Name);
        Assert.Equal(RefKind.Other, parsed[4].Kind);
    }

    [Fact]
    public void GivenDetachedHead_WhenParsing_ThenHeadDecoration()
    {
        var commits = ParseAll(Record(1, [], decorations: "HEAD"), 1000, out _);

        Assert.Equal(RefKind.Head, Assert.Single(commits[0].Decorations).Kind);
    }

    [Fact]
    public void GivenRepeatedAuthors_WhenParsing_ThenStringsAreShared()
    {
        var commits = ParseAll(Record(2, [1]) + Record(1, []), 1000, out _);

        Assert.Same(commits[0].AuthorName, commits[1].AuthorName);
        Assert.Same(commits[0].AuthorEmail, commits[1].AuthorEmail);
    }

    [Fact]
    public void GivenBrokenRecords_WhenParsing_ThenSkippedWithWarnings()
    {
        var text = "only\x1ftwo\0" + "nothex\x1f\x1fa\x1fb\x1f1\x1f\x1fs\0" + Record(5, [4]).Replace(HashFor(4), "zzz") + Record(1, []);

        var commits = ParseAll(text, 1000, out var parser);

        Assert.Single(commits);
        Assert.Equal(3, parser.Warnings.Count);
    }

    [Fact]
    public void GivenTruncatedOutput_WhenCompleting_ThenWarnsAndKeepsEarlierCommits()
    {
        var text = Record(2, [1]) + Record(1, [])[..30];

        var commits = ParseAll(text, 1000, out var parser);

        Assert.Single(commits);
        Assert.Single(parser.Warnings);
        Assert.Equal(0, parser.BufferedByteCount);
    }

    [Fact]
    public void GivenHundredThousandCommits_WhenStreamingInSmallChunks_ThenBufferStaysWithinOneRecord()
    {
        var parser = new CommitLogParser();
        var counter = new CountingCollection();
        var largestBuffer = 0;
        var batch = new StringBuilder();

        for (var number = 100_000; number >= 1; number--)
        {
            batch.Append(Record(number, number > 1 ? [number - 1] : [], subject: "Commit #" + number));
            if (batch.Length > 4000 || number == 1)
            {
                var bytes = Encoding.UTF8.GetBytes(batch.ToString());
                batch.Clear();
                for (var offset = 0; offset < bytes.Length; offset += 777)
                {
                    parser.Feed(bytes.AsSpan(offset, Math.Min(777, bytes.Length - offset)), counter);
                    largestBuffer = Math.Max(largestBuffer, parser.BufferedByteCount);
                }
            }
        }

        parser.Complete();

        Assert.Equal(100_000, counter.Count);
        Assert.Empty(parser.Warnings);
        Assert.True(largestBuffer < 400, $"buffered {largestBuffer} bytes");
    }

    private static string HashFor(int number) => number.ToString("x").PadLeft(40, '0');

    private static string Record(int number, int[] parents, string author = "Ann", string email = "ann@example.com", long time = 1_700_000_000, string decorations = "", string subject = "Subject")
    {
        var parentText = string.Join(' ', parents.Select(HashFor));
        return string.Join('\x1f', HashFor(number), parentText, author, email, time.ToString(), decorations, subject) + "\0";
    }

    private static List<CommitInfo> ParseAll(string text, int chunkSize, out CommitLogParser parser)
    {
        parser = new CommitLogParser();
        var commits = new List<CommitInfo>();
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var offset = 0; offset < bytes.Length; offset += chunkSize)
        {
            parser.Feed(bytes.AsSpan(offset, Math.Min(chunkSize, bytes.Length - offset)), commits);
        }

        parser.Complete();
        return commits;
    }

    private sealed class CountingCollection : ICollection<CommitInfo>
    {
        public int Count { get; private set; }

        public bool IsReadOnly => false;

        public void Add(CommitInfo item) => Count++;

        public void Clear() => Count = 0;

        public bool Contains(CommitInfo item) => false;

        public void CopyTo(CommitInfo[] array, int arrayIndex)
        {
        }

        public bool Remove(CommitInfo item) => false;

        public IEnumerator<CommitInfo> GetEnumerator() => Enumerable.Empty<CommitInfo>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
