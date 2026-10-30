using Gitunia.Core.Config.Models;
using Gitunia.Core.Graph;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;
using Xunit;

namespace Gitunia.Tui.Tests.Views.Lists;

[Trait("Os", "Independent")]
public sealed class GraphListSourceGlyphsTests
{
    [Fact]
    public void GivenAGraphSource_WhenReplacingTheGlyphs_ThenTheNewSetIsExposedAtOnce()
    {
        var capped = GraphGlyphSet.Unicode.WithLabelCaps(LabelCapStyle.Rounded, hasColors: true);
        using var source = new GraphListSource(new CommitGraph(), GraphGlyphSet.Unicode);

        source.Glyphs = capped;

        Assert.Equal(capped, source.Glyphs);
        Assert.True(source.Glyphs.HasLabelCaps);
    }
}
