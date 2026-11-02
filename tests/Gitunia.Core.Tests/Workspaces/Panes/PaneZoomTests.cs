using Gitunia.Core.Workspaces.Panes;
using Xunit;
using Xunit.Sdk;

namespace Gitunia.Core.Tests.Workspaces.Panes;

[Trait("Os", "Independent")]
public class PaneZoomTests
{
    [Fact]
    public void GivenAPane_WhenAskingIfItCanZoom_ThenOnlyTheMainPanesCan()
    {
        var failures = new List<string>();
        (PaneKind pane, bool expected)[] rows =
        [
            (PaneKind.Graph, true),
            (PaneKind.Detail, true),
            (PaneKind.Diff, true),
            (PaneKind.Sidebar, false),
        ];

        foreach (var (pane, expected) in rows)
        {
            try
            {
                CheckOnlyTheMainPanesCan(pane, expected);
            }
            catch (XunitException exception)
            {
                failures.Add($"{pane}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckOnlyTheMainPanesCan(PaneKind pane, bool expected)
    {
        Assert.Equal(expected, PaneZoom.CanZoom(pane));
    }

    [Fact]
    public void GivenNoZoom_WhenToggling_ThenTheFocusedPaneZooms()
    {
        Assert.Equal(PaneKind.Diff, PaneZoom.Toggle(null, PaneKind.Diff));
    }

    [Fact]
    public void GivenTheFocusedPaneIsZoomed_WhenToggling_ThenZoomEnds()
    {
        Assert.Null(PaneZoom.Toggle(PaneKind.Diff, PaneKind.Diff));
    }

    [Fact]
    public void GivenTwoPanes_WhenPairing_ThenOnlyDetailAndDiffDo()
    {
        var failures = new List<string>();
        (PaneKind a, PaneKind b, bool expected)[] rows =
        [
            (PaneKind.Detail, PaneKind.Diff, true),
            (PaneKind.Diff, PaneKind.Detail, true),
            (PaneKind.Diff, PaneKind.Diff, true),
            (PaneKind.Graph, PaneKind.Diff, false),
            (PaneKind.Detail, PaneKind.Graph, false),
        ];

        foreach (var (a, b, expected) in rows)
        {
            try
            {
                CheckOnlyDetailAndDiffDo(a, b, expected);
            }
            catch (XunitException exception)
            {
                failures.Add($"{a}, {b}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckOnlyDetailAndDiffDo(PaneKind a, PaneKind b, bool expected)
    {
        Assert.Equal(expected, PaneZoom.IsZoomPair(a, b));
    }
}
