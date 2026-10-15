using System.Globalization;
using Gitunia.Git.Commits;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class FirstParentDescendantsParserTests
{
    private static readonly ObjectId Head = Id(1);

    [Fact]
    public void GivenChainAboveHead_WhenParsing_ThenEveryCommitOfTheChainIsKept()
    {
        var output = $"{Id(2)} {Id(1)}\n{Id(3)} {Id(2)}\n{Id(4)} {Id(3)}\n";

        var descendants = FirstParentDescendantsParser.Parse(output, Head);

        Assert.Equal([Id(2), Id(3), Id(4)], Sorted(descendants));
    }

    [Fact]
    public void GivenMergeWithHeadOnlyAsSecondParent_WhenParsing_ThenItAndItsChildrenAreSkipped()
    {
        var output = $"{Id(2)} {Id(1)}\n{Id(5)} {Id(9)} {Id(2)}\n{Id(6)} {Id(5)}\n{Id(7)} {Id(2)} {Id(6)}\n";

        var descendants = FirstParentDescendantsParser.Parse(output, Head);

        Assert.Equal([Id(2), Id(7)], Sorted(descendants));
    }

    [Fact]
    public void GivenEmptyOrBrokenOutput_WhenParsing_ThenNothingIsKept()
    {
        Assert.Empty(FirstParentDescendantsParser.Parse(string.Empty, Head));
        Assert.Empty(FirstParentDescendantsParser.Parse($"not-an-id {Id(1)}\n{Id(2)}\n{Id(3)} nonsense\n", Head));
    }

    private static ObjectId[] Sorted(IEnumerable<ObjectId> ids) => [.. ids.OrderBy(id => id.ToString(), StringComparer.Ordinal)];

    private static ObjectId Id(int number) =>
        ObjectId.TryParse(number.ToString("x40", CultureInfo.InvariantCulture), out var objectId)
            ? objectId
            : throw new InvalidOperationException("Cannot build an id.");
}
