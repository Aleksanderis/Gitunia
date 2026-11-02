using System.Text;
using Gitunia.Git.Blame;
using Gitunia.Git.Blame.Models;
using Xunit;

namespace Gitunia.Git.Tests.Blame;

public class BlameParserTests
{
    private const string FirstId = "1111111111111111111111111111111111111111";
    private const string SecondId = "2222222222222222222222222222222222222222";

    private const string Output =
        $"{FirstId} 1 1 2\n"
        + "author Ann Author\nauthor-mail <ann@example.com>\nauthor-time 1700000000\nauthor-tz +0230\n"
        + "committer Ann Author\ncommitter-time 1700000000\ncommitter-tz +0230\n"
        + "summary First change\nboundary\nfilename old name.txt\n"
        + $"{SecondId} 1 3 1\n"
        + "author Bob\nauthor-time 1700003600\nauthor-tz -0100\nsummary Second: change\n"
        + "previous 3333333333333333333333333333333333333333 old name.txt\nfilename new name.txt\n"
        + $"{FirstId} 3 4 1\nfilename old name.txt\n";

    [Fact]
    public void GivenIncrementalOutput_WhenFed_ThenEachRecordIsAChunkWithItsCommit()
    {
        var parser = new BlameParser();
        var chunks = new List<BlameChunk>();

        parser.Feed(Encoding.UTF8.GetBytes(Output), chunks);
        parser.Complete();

        Assert.Equal([0, 2, 3], chunks.Select(chunk => chunk.FirstLine));
        Assert.Equal([2, 1, 1], chunks.Select(chunk => chunk.LineCount));
        Assert.Equal([0, 0, 2], chunks.Select(chunk => chunk.OriginalFirstLine));
        Assert.Empty(parser.Warnings);
        Assert.Equal("Ann Author", chunks[0].Commit.Author);
        Assert.True(chunks[0].Commit.IsBoundary);
        Assert.Equal("old name.txt", chunks[0].Commit.Path);
        Assert.Equal("Second: change", chunks[1].Commit.Summary);
        Assert.False(chunks[1].Commit.IsBoundary);
        Assert.Equal(TimeSpan.FromMinutes(-60), chunks[1].Commit.AuthorTime.Offset);
        Assert.Equal(TimeSpan.FromMinutes(150), chunks[0].Commit.AuthorTime.Offset);
        Assert.Same(chunks[0].Commit, chunks[2].Commit);
    }

    [Fact]
    public void GivenOutputCutIntoOneByteChunks_WhenFed_ThenTheChunksAreTheSame()
    {
        var parser = new BlameParser();
        var chunks = new List<BlameChunk>();

        foreach (var value in Encoding.UTF8.GetBytes(Output))
        {
            parser.Feed([value], chunks);
        }

        parser.Complete();

        Assert.Equal(3, chunks.Count);
        Assert.Empty(parser.Warnings);
    }

    [Fact]
    public void GivenUncommittedLines_WhenFed_ThenTheCommitIsUncommitted()
    {
        var parser = new BlameParser();
        var chunks = new List<BlameChunk>();

        parser.Feed(
            Encoding.UTF8.GetBytes(
                $"{BlameCommit.UncommittedId} 1 1 1\nauthor Not Committed Yet\nauthor-time 1\nauthor-tz +0000\n"
                + "summary Version of a.txt from a.txt\nfilename a.txt\n"),
            chunks);

        Assert.True(chunks.Single().Commit.IsUncommitted);
    }

    [Fact]
    public void GivenGarbageAndACutRecord_WhenFed_ThenWarningsAreReportedAndNothingThrows()
    {
        var parser = new BlameParser();
        var chunks = new List<BlameChunk>();

        parser.Feed(Encoding.UTF8.GetBytes($"not a header\n{FirstId} 1 1 1\nauthor X\n"), chunks);
        parser.Complete();

        Assert.Empty(chunks);
        Assert.Equal(2, parser.Warnings.Count);
    }

    [Fact]
    public void GivenBytes_WhenDecoding_ThenCrlfBomAndLatin1AreHandledAndBinaryIsRefused()
    {
        var crlf = BlameTextDecoder.Decode("﻿one\r\ntwo\r\n\r\nlast"u8.ToArray().Prepend((byte)0).Skip(1).ToArray());
        var latin1 = BlameTextDecoder.Decode([0x63, 0x61, 0x66, 0xE9, 0x0A]);
        var binary = BlameTextDecoder.Decode([0x01, 0x00, 0x02]);

        Assert.Equal(["one", "two", "", "last"], crlf.Lines);
        Assert.Equal(["café"], latin1.Lines);
        Assert.Equal(BlameContentKind.Binary, binary.Kind);
        Assert.Empty(binary.Lines);
    }
}
