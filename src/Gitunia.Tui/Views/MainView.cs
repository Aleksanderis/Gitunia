using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Lines;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>
/// The first screen: the chrome around the panes (tab row, frame, titles, status bar, picker), laid out like
/// <see cref="ScreenLayout"/>. The list widgets are <see cref="PaneViews"/>; this view puts them in the window and reports
/// the mouse input on the chrome.
/// </summary>
public sealed class MainView : IDisposable
{
    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private readonly ChromeParts _parts;
    private readonly WorkingTreeChrome _working;
    private readonly TabBarView _tabs;
    private readonly PickerView _picker;
    private readonly LineView _status;
    private readonly LineView _bottomEdge;
    private readonly LineView _sidebarFilter;
    private readonly FrameStripView _divider;
    private readonly FrameStripView[] _edges;
    private readonly LineView _detailTitle;
    private readonly LineView _diffTitle;
    private readonly FrameStripView _bottomDivider;

    /// <param name="parts">What the chrome is drawn from.</param>
    /// <param name="working">The chrome of the Changes view.</param>
    public MainView(ChromeParts parts, WorkingTreeChrome working)
    {
        _parts = parts;
        _working = working;
        var panes = parts.Panes;
        var geometry = parts.Geometry;
        var content = parts.Content;
        var glyphs = panes.Content.Glyphs;
        _tabs = new TabBarView(content, glyphs)
        {
            X = 0,
            Y = ScreenLayout.TabRow,
            Width = Dim.Fill(),
            Height = 1,
        };
        _sidebarFilter = new LineView(new SidebarFilterLine(content, glyphs))
        {
            X = 1,
            Y = Pos.Func(_ => geometry.Rects().SidebarFilter.Y),
            Width = Dim.Func(_ => geometry.Rects().SidebarFilter.Width),
            Height = Dim.Func(_ => geometry.Rects().SidebarFilter.Height),
        };
        _edges =
        [
            new FrameStripView(parts.Grids) { X = 0, Y = ScreenLayout.FrameTop, Width = Dim.Fill(), Height = 1 },
            CreateSideEdge(Pos.Absolute(0)),
            CreateSideEdge(Pos.Func(_ => Math.Max(0, geometry.ScreenWidth - 1))),
        ];
        _bottomEdge = new LineView(new BottomEdgeLine(parts))
        {
            X = 0,
            Y = Pos.Func(_ => ScreenLayout.FrameBottom(geometry.ScreenHeight)),
            Width = Dim.Fill(),
            Height = 1,
        };
        _divider = new FrameStripView(parts.Grids)
        {
            X = Pos.Func(_ => geometry.Rects().SidebarDivider.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => geometry.Rects().SidebarDivider.Width),
            Height = Dim.Func(_ => geometry.Rects().SidebarDivider.Height),
        };
        _detailTitle = CreateTitle(new PaneTitleLine(parts, PaneTitleKind.Detail));
        _diffTitle = CreateTitle(new PaneTitleLine(parts, PaneTitleKind.Diff));
        _bottomDivider = new FrameStripView(parts.Grids)
        {
            X = Pos.Func(_ => geometry.Rects().BottomDivider.X),
            Y = Pos.Func(_ => geometry.Rects().BottomDivider.Y),
            Width = Dim.Func(_ => geometry.Rects().BottomDivider.Width),
            Height = Dim.Func(_ => geometry.Rects().BottomDivider.Height),
        };
        _picker = new PickerView(content, glyphs)
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => ScreenLayout.InnerWidth(geometry.ScreenWidth)),
            Height = Dim.Func(_ => ScreenLayout.GraphHeight(geometry.ScreenHeight)),
            Visible = false,
            Gate = parts.Gate,
        };
        _picker.SetScheme(Palette.BaseScheme());
        _status = new LineView(new StatusBarLine(content, geometry, glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };

        WireGestures();
        AddViews();
        ApplyVisibility();
        panes.Chrome.RefreshRequested += RefreshChrome;
    }

    /// <summary>
    /// Raised for a mouse gesture the mouse map decides on: the keymap view name of the pane (or <c>Tabs</c>, <c>Picker</c>),
    /// the gesture and, for the tab row, the one-based number of the clicked tab.
    /// </summary>
    public event Action<string, MouseGesture, int?>? Gestured;

    /// <summary>
    /// Raised before the click gesture on the message box, with the text column and row of the pointer (the box's padding
    /// removed) and the text width of the box.
    /// </summary>
    public event Action<int, int, int>? MessageClicked
    {
        add => _working.MessageClicked += value;
        remove => _working.MessageClicked -= value;
    }

    /// <summary>
    /// Raised while the message text is dragged over with the button held, with the same arguments as
    /// <see cref="MessageClicked"/>.
    /// </summary>
    public event Action<int, int, int>? MessageDragged
    {
        add => _working.MessageDragged += value;
        remove => _working.MessageDragged -= value;
    }

    /// <summary>Raised while a divider or a pane title row is dragged: which edge and the pointer's screen column and row.</summary>
    public event Action<PaneEdge, int, int>? EdgeDragged;

    /// <summary>Raised with the frame line and the pointer column when the overlay box is clicked.</summary>
    public event Action<int, int>? OverlayLineClicked;

    /// <summary>Raised with the frame line and column when a drag begins on the overlay box.</summary>
    public event Action<int, int>? OverlayDragStarted;

    /// <summary>Raised with the frame line and column of the pointer while a drag goes on over the box.</summary>
    public event Action<int, int>? OverlayDragged;

    /// <summary>Raised when the button is released after a drag over the overlay box.</summary>
    public event Action? OverlayDragReleased;

    /// <summary>Raised with the frame line and the pointer column when the overlay box is double-clicked.</summary>
    public event Action<int, int>? OverlayLineDoubleClicked;

    /// <summary>Raised with the frame line and the pointer column on a triple-click of the overlay box.</summary>
    public event Action<int, int>? OverlayLineTripleClicked;

    /// <summary>Raised with the list line when a picker row, or its more or less line, is clicked (the highlight follows the pointer).</summary>
    public event Action<PickerListEntry>? PickerRowClicked
    {
        add => _picker.RowClicked += value;
        remove => _picker.RowClicked -= value;
    }

    /// <summary>Raised after the chrome was redrawn: the model a popup is built from may have changed.</summary>
    public event Action? ChromeRefreshed;

    /// <summary>True while panes are shown or hidden: the toolkit moves the focus on its own then, which is not the user's choice.</summary>
    public bool IsChangingVisibility { get; private set; }

    /// <summary>Shows the repository picker in place of the graph, or the graph again.</summary>
    public void ShowPicker(bool isOpen)
    {
        if (_picker.Visible == isOpen)
        {
            return;
        }

        _parts.Mode.IsPicker = isOpen;
        _picker.Visible = isOpen;
        ApplyVisibility();
        if (!isOpen)
        {
            FocusedBasePane().SetFocus();
        }

        _picker.SetNeedsDraw();
        _parts.Panes.Graph.SetNeedsDraw();
    }

    /// <summary>Shows the Changes view in place of the sidebar, graph and detail panes, or those again.</summary>
    public void ShowWorkingTree(bool isShown)
    {
        if (_parts.Mode.IsWorking == isShown)
        {
            return;
        }

        _parts.Mode.IsWorking = isShown;
        ApplyVisibility();
        _parts.Grids.Reset();
        _parts.Panes.Window.SetNeedsLayout();
        _parts.Panes.Window.SetNeedsDraw();
    }

    /// <summary>Redraws the tab row, frame and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        ChromeRefreshed?.Invoke();
        _tabs.SetNeedsDraw();
        _status.SetNeedsDraw();
        _picker.SetNeedsDraw();
        _sidebarFilter.SetNeedsDraw();
        _divider.SetNeedsDraw();
        _bottomEdge.SetNeedsDraw();
        foreach (var edge in _edges)
        {
            edge.SetNeedsDraw();
        }

        _parts.Panes.Sidebar.SetNeedsDraw();
        _detailTitle.SetNeedsDraw();
        _diffTitle.SetNeedsDraw();
        _bottomDivider.SetNeedsDraw();
        if (_parts.Mode.IsWorking)
        {
            _working.Refresh();
            RedrawWorkingPanes();
        }
    }

    /// <summary>Repaints every view after the palette changed (the settings screen previews a theme).</summary>
    public void RefreshPalette()
    {
        _parts.Panes.ApplyPaneSchemes();
        _picker.SetScheme(Palette.BaseScheme());
        RefreshChrome();
        _parts.Panes.Window.SetNeedsDraw();
        foreach (var pane in _parts.Panes.Lists)
        {
            pane.SetNeedsDraw();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _working.Dispose();
        _parts.Panes.Window.Dispose();
    }

    private PaneListView FocusedBasePane()
    {
        if (!_parts.Mode.IsWorking)
        {
            return _parts.Panes.Graph;
        }

        return _parts.Geometry.WorkingFocus switch
        {
            PaneKind.WorkingStaged => _parts.Panes.WorkingStaged,
            PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => _parts.Panes.WorkingDiff,
            PaneKind.WorkingMessage => _parts.Panes.WorkingMessage,
            _ => _parts.Panes.WorkingUnstaged,
        };
    }

    private void RedrawWorkingPanes()
    {
        _parts.Panes.WorkingUnstaged.SetNeedsDraw();
        _parts.Panes.WorkingStaged.SetNeedsDraw();
        _parts.Panes.WorkingDiff.SetNeedsDraw();
        _parts.Panes.WorkingMessage.SetNeedsDraw();
    }

    private FrameStripView CreateSideEdge(Pos x) => new(_parts.Grids)
    {
        X = x,
        Y = ScreenLayout.GraphTop,
        Width = 1,
        Height = Dim.Func(_ => ScreenLayout.GraphHeight(_parts.Geometry.ScreenHeight)),
    };

    private static LineView CreateTitle(PaneTitleLine title) =>
        new(title)
        {
            X = Pos.Func(_ => title.Rect().X),
            Y = Pos.Func(_ => title.Rect().Y),
            Width = Dim.Func(_ => title.Rect().Width),
            Height = Dim.Func(_ => title.Rect().Height),
        };

    /// <summary>Cancels a tab chip drag in progress (<c>Esc</c>); false when none is going on.</summary>
    public bool CancelTabDrag() => _tabs.CancelDrag();

    private void WireGestures()
    {
        var panes = _parts.Panes;
        _tabs.TabClicked += number => Gestured?.Invoke(DefaultMouseMap.TabsView, MouseGesture.Click, number);
        _tabs.TabDragged += (gesture, places) => Gestured?.Invoke(DefaultMouseMap.TabsView, gesture, places);
        _tabs.DirtyMarkerClicked += number => Gestured?.Invoke(DefaultMouseMap.TabDirtyMarker, MouseGesture.Click, number);
        _tabs.RemoteChipClicked += view => Gestured?.Invoke(view, MouseGesture.Click, null);
        _tabs.NewTabClicked += () => Gestured?.Invoke(DefaultMouseMap.NewTabButton, MouseGesture.Click, null);
        _tabs.CommandsClicked += () => Gestured?.Invoke(DefaultMouseMap.CommandsButton, MouseGesture.Click, null);
        _picker.GestureRaised += gesture => Gestured?.Invoke(DefaultKeymap.PickerView, gesture, null);
        _picker.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.Graph.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.GraphView, gesture, target);
        panes.Sidebar.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.SidebarView, gesture, target);
        panes.Sidebar.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.Detail.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DetailView, gesture, target);
        panes.Detail.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.Diff.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DiffView, gesture, target);
        _working.Gestured += (view, gesture, target) => Gestured?.Invoke(view, gesture, target);
        _divider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Sidebar, column, row);
        _working.ListsDragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.WorkingLists, column, row);
        _bottomDivider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Detail, column, row);
        _detailTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _diffTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _bottomEdge.Clicked += OnBottomEdgeClicked;
        _detailTitle.Clicked += OnDetailTitleClicked;
        _diffTitle.Clicked += OnDiffTitleClicked;
        panes.Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line, panes.Overlay.PointerColumn);
        panes.Overlay.DragStarted += (line, column) => OverlayDragStarted?.Invoke(line, column);
        panes.Overlay.Dragged += (line, column) => OverlayDragged?.Invoke(line, column);
        panes.Overlay.DragReleased += () => OverlayDragReleased?.Invoke();
        panes.Overlay.LineDoubleClicked += line => OverlayLineDoubleClicked?.Invoke(line, panes.Overlay.PointerColumn);
        panes.Overlay.LineTripleClicked += line => OverlayLineTripleClicked?.Invoke(line, panes.Overlay.PointerColumn);
        panes.Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);
    }

    private void AddViews()
    {
        var panes = _parts.Panes;
        panes.Window.Add(
            _tabs,
            _edges[0],
            _bottomEdge,
            _edges[1],
            _edges[2],
            panes.Sidebar,
            _sidebarFilter,
            _divider,
            panes.Graph,
            _detailTitle,
            panes.Detail,
            _bottomDivider,
            _diffTitle,
            panes.Diff);
        foreach (var view in _working.FrameViews)
        {
            panes.Window.Add(view);
        }

        panes.Window.Add(panes.WorkingUnstaged, panes.WorkingStaged, panes.WorkingDiff, panes.WorkingMessage);
        foreach (var view in _working.RowViews)
        {
            panes.Window.Add(view);
        }

        panes.Window.Add(_picker, _status, panes.Overlay);
    }

    // The history panes and the Changes view panes never show together; the picker covers either one. The panes that appear are
    // shown before the others are hidden, so the toolkit never hands the focus of a hiding pane to a pane that is about to hide too.
    private void ApplyVisibility()
    {
        IsChangingVisibility = true;
        try
        {
            SetVisibility();
        }
        finally
        {
            IsChangingVisibility = false;
        }
    }

    private void SetVisibility()
    {
        var panes = _parts.Panes;
        var isPicker = _picker.Visible;
        var isWorking = _parts.Mode.IsWorking;
        var history = !isWorking;
        _working.SetVisibility(isWorking, isPicker);
        panes.WorkingUnstaged.Visible = isWorking && !isPicker;
        panes.WorkingStaged.Visible = isWorking && !isPicker;
        panes.WorkingDiff.Visible = isWorking && !isPicker;
        panes.WorkingMessage.Visible = isWorking && !isPicker;
        panes.Sidebar.Visible = history;
        _sidebarFilter.Visible = history;
        _divider.Visible = history;
        _detailTitle.Visible = history;
        panes.Detail.Visible = history;
        _bottomDivider.Visible = history;
        _diffTitle.Visible = history;
        panes.Diff.Visible = history;
        panes.Graph.Visible = history && !isPicker;
    }

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!_parts.Gate.IsSuppressed && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnDetailTitleClicked(int column)
    {
        if (_parts.Gate.IsSuppressed)
        {
            return;
        }

        var panes = _parts.Panes;
        FocusFromTitle(panes.Detail);
        var selectedTab = (panes.Detail.Source as DetailListSource)?.Content.Tab ?? BottomTab.Changes;
        if (TitleButton.ViewAt(DetailTabStrip.Layout(_detailTitle.Viewport.Width, selectedTab), column) is { } tabView)
        {
            Gestured?.Invoke(tabView, MouseGesture.Click, null);
        }
        else if ((panes.Detail.Source as DetailListSource)?.Content is { } content
            && DetailPaneRenderer.ViewButton(
                content,
                _detailTitle.Viewport.Width,
                panes.Content.Glyphs,
                panes.Content.Keys) is { } chip
            && column >= chip.Start
            && column < chip.Start + chip.Width)
        {
            Gestured?.Invoke(chip.View, MouseGesture.Click, null);
        }
    }

    private void OnDiffTitleClicked(int column)
    {
        if (_parts.Gate.IsSuppressed)
        {
            return;
        }

        var panes = _parts.Panes;
        FocusFromTitle(panes.Diff);
        var buttons = DiffTitleButtons.Layout(
            _diffTitle.Viewport.Width,
            panes.Content.Keys,
            panes.Diff.Source is DiffListSource { Document.IsFileContent: true });
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    // A click on the bottom edge acts on the in-progress operation banner drawn over it (ux-workflows.md, workflow 4).
    private void OnBottomEdgeClicked(int column)
    {
        var model = _parts.Content.Model();
        var geometry = _parts.Geometry;
        if (_parts.Gate.IsSuppressed
            || model.Banner is not { } banner
            || geometry.ScreenHeight < ScreenLayout.MinimumHeightWithBanner)
        {
            return;
        }

        if (BannerRenderer.Build(banner, geometry.ScreenWidth, _parts.Panes.Content.Glyphs).ViewAt(column) is { } view)
        {
            Gestured?.Invoke(view, MouseGesture.Click, null);
        }
    }
}
