using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.Core.Tests.Diff;

[Trait("Os", "Independent")]
public class ChangedSpanTests
{
    [Fact]
    public void GivenOverlappingAndTouchingSpans_WhenUnited_ThenTheyAreJoinedInOrder()
    {
        ChangedSpan[] first = [new(10, 3), new(0, 2)];
        ChangedSpan[] second = [new(1, 3), new(13, 2), new(20, 1)];

        var union = ChangedSpan.Union([first[1], first[0]], second);

        Assert.Equal([new ChangedSpan(0, 4), new ChangedSpan(10, 5), new ChangedSpan(20, 1)], union);
    }

    [Fact]
    public void GivenOneListEmpty_WhenUnited_ThenTheOtherIsReturned()
    {
        ChangedSpan[] spans = [new(2, 1)];

        Assert.Equal(spans, ChangedSpan.Union(spans, []));
        Assert.Equal(spans, ChangedSpan.Union([], spans));
    }
}
