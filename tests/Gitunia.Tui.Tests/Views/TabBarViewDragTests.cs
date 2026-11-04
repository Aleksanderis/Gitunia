using System.Drawing;
using Gitunia.Core.Mouse;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;
using Gitunia.Tui.Views;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Views;

[Trait("Os", "Independent")]
public sealed class TabBarViewDragTests
{
    private const int RowWidth = 100;

    [Fact]
    public void GivenAPlainClickOnAChip_WhenReleased_ThenOnlyTheTabClickIsRaised()
    {
        using var fixture = new Fixture();

        fixture.Click(fixture.ColumnOf(2));

        Assert.Equal([2], fixture.Clicked);
        Assert.Empty(fixture.Drags);
    }

    [Fact]
    public void GivenAPressedChip_WhenThePointerMovesWithinTheChip_ThenNothingMoves()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(1);

        fixture.Press(start);
        fixture.Move(start + 1);
        fixture.Release(start + 1);

        Assert.Empty(fixture.Drags);
        Assert.Equal(["alpha", "beta", "gamma"], fixture.Names);
    }

    [Fact]
    public void GivenAChipDraggedRight_WhenThePointerCrossesTheNeighbour_ThenTheTabMovesOnePlaceAndIsActivated()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(1);

        fixture.Press(start);
        fixture.Move(start + 1);
        fixture.Move(fixture.ColumnOf(2) + 8);
        fixture.Release(fixture.ColumnOf(2) + 8);

        Assert.Equal([1], fixture.Clicked);
        Assert.Equal([MouseGesture.DragForward], fixture.Drags);
        Assert.Equal(["beta", "alpha", "gamma"], fixture.Names);
    }

    [Fact]
    public void GivenAChipDraggedAcrossAllTabs_WhenThePointerFollows_ThenItEndsLastWithoutMovingTwicePerStep()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(1);

        fixture.Press(start);
        fixture.Move(start + 1);
        for (var column = start + 2; column < RowWidth - 20; column++)
        {
            fixture.Move(column);
        }

        fixture.Release(RowWidth - 21);

        Assert.Equal(["beta", "gamma", "alpha"], fixture.Names);
    }

    [Fact]
    public void GivenAChipDraggedLeft_WhenThePointerCrossesTheNeighbour_ThenTheTabMovesLeft()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(3);

        fixture.Press(start);
        fixture.Move(start - 1);
        fixture.Move(fixture.ColumnOf(2) + 1);
        fixture.Release(fixture.ColumnOf(2) + 1);

        Assert.Equal(["alpha", "gamma", "beta"], fixture.Names);
    }

    [Fact]
    public void GivenADragReleasedOutsideTheRow_WhenItEnds_ThenTheTabGoesBack()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(1);

        fixture.Press(start);
        fixture.Move(start + 1);
        fixture.Move(fixture.ColumnOf(2) + 8);
        fixture.Release(fixture.ColumnOf(2) + 8, row: 5);

        Assert.Equal(["alpha", "beta", "gamma"], fixture.Names);
    }

    [Fact]
    public void GivenADragInProgress_WhenItIsCancelled_ThenTheTabGoesBackAndTheRestOfTheDragDoesNothing()
    {
        using var fixture = new Fixture();
        var start = fixture.ColumnOf(1);
        fixture.Press(start);
        fixture.Move(start + 1);
        fixture.Move(fixture.ColumnOf(2) + 8);
        Assert.Equal(["beta", "alpha", "gamma"], fixture.Names);

        var cancelled = fixture.View.CancelDrag();
        fixture.Move(fixture.ColumnOf(1) + 8);
        fixture.Release(fixture.ColumnOf(1) + 8);

        Assert.True(cancelled);
        Assert.False(fixture.View.CancelDrag());
        Assert.Equal(["alpha", "beta", "gamma"], fixture.Names);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TabContent _content = new();

        public Fixture()
        {
            View = new TabBarView(_content, GraphGlyphSet.Unicode) { Width = RowWidth, Height = 1 };
            View.Layout();
            View.TabClicked += OnClicked;
            View.TabDragged += OnDragged;
        }

        public TabBarView View { get; }

        public List<int> Clicked { get; } = [];

        public List<MouseGesture> Drags { get; } = [];

        public IReadOnlyList<string> Names => _content.Names;

        public int ColumnOf(int number) =>
            TabBarLayout.Layout(_content.Model().Tabs!, RowWidth, GraphGlyphSet.Unicode, null)
                .First(cell => cell.Number == number).Start + 3;

        public void Press(int column) => Send(MouseFlags.LeftButtonPressed, column, 0);

        public void Move(int column) => Send(MouseFlags.LeftButtonPressed | MouseFlags.PositionReport, column, 0);

        public void Release(int column, int row = 0) => Send(MouseFlags.LeftButtonReleased, column, row);

        public void Click(int column)
        {
            Press(column);
            Release(column);
            Send(MouseFlags.LeftButtonClicked, column, 0);
        }

        public void Dispose() => View.Dispose();

        private void Send(MouseFlags flags, int column, int row) =>
            View.NewMouseEvent(new Mouse { Flags = flags, Position = new Point(column, row) });

        private void OnClicked(int number)
        {
            Clicked.Add(number);
            _content.Activate(number - 1);
        }

        // Stands in for the state layer: a move gesture reorders the tabs the view then reads.
        private void OnDragged(MouseGesture gesture, int places)
        {
            Drags.Add(gesture);
            _content.Move(gesture == MouseGesture.DragBack ? -places : places);
        }
    }

    private sealed class TabContent : IScreenContent
    {
        private readonly List<string> _names = ["alpha", "beta", "gamma"];
        private int _active;

        public IReadOnlyList<string> Names => _names;

        public ScreenModel Model()
        {
            var labels = _names
                .Select((name, index) => new TabLabel(index + 1, name, null, index == _active, false, 0, 0, false, false))
                .ToList();

            return new ScreenModel("repo", null, 1, labels.Count, "Normal", string.Empty, 0, 0, false, string.Empty, Tabs: labels);
        }

        public void Activate(int index) => _active = index;

        public void Move(int offset)
        {
            var target = Math.Clamp(_active + offset, 0, _names.Count - 1);
            var name = _names[_active];
            _names.RemoveAt(_active);
            _names.Insert(target, name);
            _active = target;
        }

        public StyledLine DetailTitle(int width) => new([]);

        public StyledLine DiffTitle(int width) => new([]);

        public SidebarChrome SidebarFilter() => new(string.Empty, false, false);

        public string WorkingDiffTitle() => string.Empty;
    }
}
