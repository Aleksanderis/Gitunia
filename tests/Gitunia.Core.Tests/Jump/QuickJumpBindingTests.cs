using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Jump;

[Trait("Os", "Independent")]
public class QuickJumpBindingTests
{
    [Theory]
    [InlineData(DefaultKeymap.GraphView)]
    [InlineData(DefaultKeymap.DiffView)]
    [InlineData(DefaultKeymap.WorkingUnstagedView)]
    [InlineData(DefaultKeymap.WorkingStagedView)]
    [InlineData(DefaultKeymap.WorkingUnstagedDiffView)]
    [InlineData(DefaultKeymap.WorkingStagedDiffView)]
    public void GivenAView_WhenListingItsPaletteEntries_ThenTheQuickJumpIsThereOnSpaceJ(string view)
    {
        var entries = PaletteEntryBuilder.Build(DefaultKeymap.Create(), view);

        var entry = Assert.Single(entries, candidate => candidate.Action == ActionIds.QuickJump);
        Assert.Contains("<space>j", entry.Keys, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(entry.Title));
    }
}
