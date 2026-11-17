using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Overlays;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Overlays;

/// <summary>
/// The window half of the list contract (see <c>ListViewportContractTests</c> in Core): every renderer that windows a
/// list keeps the selected row inside the rows it shows at the top, in the middle, at the end, in a smaller window and
/// for an empty list. A new windowing renderer adds a row to <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class ListWindowContractTests
{
    private const int Items = 40;

    public static TheoryData<string> Surfaces => ["grouped-rows-plain", "grouped-rows-grouped"];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAnySelection_WhenWindowing_ThenTheSelectedLineIsShown(string surface)
    {
        foreach (var rows in new[] { 12, 5, 1 })
        {
            foreach (var selected in new[] { 0, 1, Items / 2, Items - 2, Items - 1 })
            {
                var window = WindowOf(surface, Items, selected, rows);

                Assert.InRange(window.Selected, window.Top, window.Top + window.RowCount - 1);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAnEmptyList_WhenWindowing_ThenOneRowAtZeroShows(string surface)
    {
        var window = WindowOf(surface, 0, 0, 8);

        Assert.Equal(1, window.RowCount);
        Assert.Equal(0, window.Top);
    }

    private static ListWindow WindowOf(string surface, int count, int selected, int rows)
    {
        var grouped = surface == "grouped-rows-grouped";
        var entries = Enumerable.Range(0, count)
            .Select(index => new PaletteEntry(
                ActionIds.RepositoryRefresh,
                $"entry{index}",
                string.Empty,
                Group: grouped ? $"group{index / 5}" : null))
            .ToList();

        return GroupedRows.Of(entries, rows, scrolls: true).Window(selected, rows);
    }
}
