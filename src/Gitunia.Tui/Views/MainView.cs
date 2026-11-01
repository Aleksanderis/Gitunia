using Gitunia.Core.Conflicts;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: tab row, the framed panes and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly TabBarView _tabs;
    private readonly PickerView _picker;
    private readonly LineView _status;
    private readonly LineView _bottomEdge;
    private readonly LineView _sidebarFilter;
    private readonly FrameStripView _divider;
    private readonly FrameStripView[] _edges;
    private readonly LineView _detailTitle;
    private readonly LineView _diffTitle;
    private readonly FrameStripView _bottomDividerView;
    private readonly FrameStripView _workingDivider;
    private readonly FrameStripView _workingLeftEdge;
    private readonly FrameStripView _workingRightEdge;
    private readonly LineView _messageOptions;
    private readonly LineView _workingDiffTitle;
    private readonly LineView _stageAllButton;
    private readonly LineView _unstageAllButton;
    private readonly LineView _unstagedViewButton;
    private readonly LineView _resolveButton;
    private readonly LineView _stagedViewButton;
    private readonly LineView _unstagedDiffButton;
    private readonly LineView _stagedDiffButton;
    private bool _isWorkingMode;
    private WorkingGridKey? _workingGridKey;
    private FrameGrid? _workingGrid;

    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    private readonly Func<ScreenModel> _model;
    private readonly GraphGlyphSet _glyphs;
    private readonly Func<PaneArrangement> _arrangement;
    private Func<bool> _suppressed = () => false;
    private GridKey? _gridKey;
    private FrameGrid? _grid;

    public MainView(
        Func<ScreenModel> model,
        Func<int, StyledLine> detailTitleLine,
        GraphGlyphSet glyphs,
        SidebarListSource sidebarSource,
        Func<SidebarChrome> sidebarChrome,
        Func<PaneArrangement> arrangement,
        Func<int, StyledLine> diffTitleLine,
        DiffListSource diffSource)
    {
        _arrangement = arrangement;
        _model = model;
        _glyphs = glyphs;

        _tabs = new TabBarView(model, glyphs)
        {
            X = 0,
            Y = ScreenLayout.TabRow,
            Width = Dim.Fill(),
            Height = 1,
        };

        Sidebar = new PaneListView
        {
            OpensRowMenu = true,
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Sidebar.Width),
            Height = Dim.Func(_ => Rects().Sidebar.Height),
            CanFocus = true,
            Source = sidebarSource,
        };

        _sidebarFilter = new LineView(width =>
        {
            var chrome = sidebarChrome();
            return PanePadding.Wrap(SidebarRowRenderer.FilterLine(chrome.FilterText, chrome.IsEditingFilter, PanePadding.TextWidth(width), glyphs));
        })
        {
            X = 1,
            Y = Pos.Func(_ => Rects().SidebarFilter.Y),
            Width = Dim.Func(_ => Rects().SidebarFilter.Width),
            Height = Dim.Func(_ => Rects().SidebarFilter.Height),
        };

        _edges =
        [
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.FrameTop, Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
            new FrameStripView(CurrentGrid) { X = Pos.Func(_ => Math.Max(0, ScreenWidth - 1)), Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
        ];

        _bottomEdge = new LineView(BottomEdgeLine)
        {
            X = 0,
            Y = Pos.Func(_ => ScreenLayout.FrameBottom(ScreenHeight())),
            Width = Dim.Fill(),
            Height = 1,
        };

        _divider = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().SidebarDivider.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().SidebarDivider.Width),
            Height = Dim.Func(_ => Rects().SidebarDivider.Height),
        };

        Graph = new PaneListView
        {
            X = Pos.Func(_ => Rects().Graph.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Graph.Width),
            Height = Dim.Func(_ => Rects().Graph.Height),
            CanFocus = true,
            IgnoresBlankDoubleClick = true,
        };

        _detailTitle = new LineView(width => CurrentGrid().ResolveRules(detailTitleLine(width), Rects().DetailTitle.Y, Rects().DetailTitle.X))
        {
            X = Pos.Func(_ => Rects().DetailTitle.X),
            Y = Pos.Func(_ => Rects().DetailTitle.Y),
            Width = Dim.Func(_ => Rects().DetailTitle.Width),
            Height = Dim.Func(_ => Rects().DetailTitle.Height),
        };

        Detail = new PaneListView
        {
            X = Pos.Func(_ => Rects().Detail.X),
            Y = Pos.Func(_ => Rects().Detail.Y),
            Width = Dim.Func(_ => Rects().Detail.Width),
            Height = Dim.Func(_ => Rects().Detail.Height),
            CanFocus = true,
        };

        _bottomDividerView = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().BottomDivider.X),
            Y = Pos.Func(_ => Rects().BottomDivider.Y),
            Width = Dim.Func(_ => Rects().BottomDivider.Width),
            Height = Dim.Func(_ => Rects().BottomDivider.Height),
        };

        _diffTitle = new LineView(width => CurrentGrid().ResolveRules(diffTitleLine(width), Rects().DiffTitle.Y, Rects().DiffTitle.X))
        {
            X = Pos.Func(_ => Rects().DiffTitle.X),
            Y = Pos.Func(_ => Rects().DiffTitle.Y),
            Width = Dim.Func(_ => Rects().DiffTitle.Width),
            Height = Dim.Func(_ => Rects().DiffTitle.Height),
        };

        Diff = new PaneListView
        {
            X = Pos.Func(_ => Rects().Diff.X),
            Y = Pos.Func(_ => Rects().Diff.Y),
            Width = Dim.Func(_ => Rects().Diff.Width),
            Height = Dim.Func(_ => Rects().Diff.Height),
            CanFocus = true,
            Source = diffSource,
        };

        UnstagedSource = new WorkingTreeListSource(WorkingTreeSection.Unstaged, glyphs);
        StagedSource = new WorkingTreeListSource(WorkingTreeSection.Staged, glyphs);
        WorkingUnstaged = CreateWorkingList(UnstagedSource, () => WorkingRects().Unstaged);
        WorkingStaged = CreateWorkingList(StagedSource, () => WorkingRects().Staged);

        _workingDivider = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => WorkingDividerRect().X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => WorkingDividerRect().Width),
            Height = Dim.Func(_ => WorkingDividerRect().Height),
            Visible = false,
        };

        _workingLeftEdge = CreateWorkingEdge(() => WorkingEdgeRect(WorkingRects().Unstaged, WorkingRects().Staged, isLeft: true));
        _workingRightEdge = CreateWorkingEdge(() => WorkingEdgeRect(WorkingRects().Diff, WorkingRects().Message, isLeft: false));

        _workingDiffTitle = new LineView(WorkingDiffTitleLine)
        {
            X = Pos.Func(_ => WorkingRects().Diff.X),
            Y = Pos.Func(_ => WorkingRects().Diff.Y - 1),
            Width = Dim.Func(_ => WorkingRects().Diff.Width),
            Height = Dim.Func(_ => WorkingRects().Diff.IsEmpty ? 0 : 1),
            Visible = false,
        };

        WorkingDiff = new PaneListView
        {
            X = Pos.Func(_ => WorkingRects().Diff.X),
            Y = Pos.Func(_ => WorkingRects().Diff.Y),
            Width = Dim.Func(_ => WorkingRects().Diff.Width),
            Height = Dim.Func(_ => WorkingRects().Diff.Height),
            CanFocus = true,
            Source = new DiffListSource(glyphs) { GroupKeys = () => _model().Keys },
            Visible = false,
        };

        WorkingMessage = new PaneListView
        {
            X = Pos.Func(_ => WorkingRects().MessageEditor.X),
            Y = Pos.Func(_ => WorkingRects().MessageEditor.Y),
            Width = Dim.Func(_ => WorkingRects().MessageEditor.Width),
            Height = Dim.Func(_ => WorkingRects().MessageEditor.Height),
            CanFocus = true,
            Source = new MessageBoxListSource(() => ActiveDraft(), () => IsEditingMessage(), glyphs),
            Visible = false,
        };

        _messageOptions = new LineView(OptionsLine)
        {
            X = Pos.Func(_ => WorkingRects().MessageOptions.X),
            Y = Pos.Func(_ => WorkingRects().MessageOptions.Y),
            Width = Dim.Func(_ => WorkingRects().MessageOptions.Width),
            Height = Dim.Func(_ => WorkingRects().MessageOptions.Height),
            Visible = false,
        };

        _messageOptions.Clicked += OnMessageOptionsClicked;
        _stageAllButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, kind: ListButtonKind.StageAll);
        _unstageAllButton = CreateListButton(WorkingTreeSection.Staged, WorkingStaged, kind: ListButtonKind.StageAll);
        _unstagedViewButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, kind: ListButtonKind.View);
        _stagedViewButton = CreateListButton(WorkingTreeSection.Staged, WorkingStaged, kind: ListButtonKind.View);
        _resolveButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, kind: ListButtonKind.Resolve);
        _unstagedDiffButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, kind: ListButtonKind.Diff);
        _stagedDiffButton = CreateListButton(WorkingTreeSection.Staged, WorkingStaged, kind: ListButtonKind.Diff);

        _picker = new PickerView(model, glyphs)
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => ScreenLayout.InnerWidth(ScreenWidth)),
            Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())),
            Visible = false,
        };

        ApplyPaneSchemes();

        _status = new LineView(width => ChromeBuilder.Status(BannerRenderer.ForStatusBar(model(), ScreenHeight()), width, glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };

        Window = new Window { Title = string.Empty, BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        Overlay = new OverlayView();
        _tabs.TabClicked += number => Gestured?.Invoke(DefaultMouseMap.TabsView, MouseGesture.Click, number);
        _tabs.DirtyMarkerClicked += number => Gestured?.Invoke(DefaultMouseMap.TabDirtyMarker, MouseGesture.Click, number);
        _tabs.RemoteChipClicked += view => Gestured?.Invoke(view, MouseGesture.Click, null);
        _tabs.NewTabClicked += () => Gestured?.Invoke(DefaultMouseMap.NewTabButton, MouseGesture.Click, null);
        _tabs.CommandsClicked += () => Gestured?.Invoke(DefaultMouseMap.CommandsButton, MouseGesture.Click, null);
        _picker.GestureRaised += gesture => Gestured?.Invoke(DefaultKeymap.PickerView, gesture, null);
        _picker.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Graph.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.GraphView, gesture, target);
        Sidebar.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.SidebarView, gesture, target);
        Sidebar.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Detail.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DetailView, gesture, target);
        Diff.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DiffView, gesture, target);

        WorkingUnstaged.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingUnstagedView, gesture, target);
        WorkingStaged.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingStagedView, gesture, target);
        WorkingDiff.GestureRaised += (gesture, target) => Gestured?.Invoke(WorkingDiffView(), gesture, target);
        WorkingDiff.ButtonAt = (column, row) => (WorkingDiff.Source as DiffListSource)?.ButtonAt(WorkingDiff, column, row);
        WorkingDiff.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        WorkingMessage.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingMessageView, gesture, target);
        WorkingMessage.ClickPositioned += OnMessageClicked;
        _divider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Sidebar, column, row);
        _bottomDividerView.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Detail, column, row);
        _detailTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _diffTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _bottomEdge.Clicked += OnBottomEdgeClicked;
        _detailTitle.Clicked += OnDetailTitleClicked;
        _diffTitle.Clicked += OnDiffTitleClicked;
        _workingDiffTitle.Clicked += OnWorkingDiffTitleClicked;
        Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line);
        Overlay.DragStarted += (line, column) => OverlayDragStarted?.Invoke(line, column);
        Overlay.Dragged += line => OverlayDragged?.Invoke(line);
        Overlay.LineDoubleClicked += line => OverlayLineDoubleClicked?.Invoke(line);
        Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);

        Window.Add(
            _tabs,
            _edges[0],
            _bottomEdge,
            _edges[1],
            _edges[2],
            Sidebar,
            _sidebarFilter,
            _divider,
            Graph,
            _detailTitle,
            Detail,
            _bottomDividerView,
            _diffTitle,
            Diff,
            _workingDivider,
            _workingLeftEdge,
            _workingRightEdge,
            _workingDiffTitle,
            WorkingUnstaged,
            WorkingStaged,
            WorkingDiff,
            WorkingMessage,
            _messageOptions,
            _stageAllButton,
            _unstageAllButton,
            _unstagedViewButton,
            _resolveButton,
            _stagedViewButton,
            _unstagedDiffButton,
            _stagedDiffButton,
            _picker,
            _status,
            Overlay);

        ApplyVisibility();
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
    public event Action<int, int, int>? MessageClicked;

    /// <summary>Raised while a divider or a pane title row is dragged: which edge and the pointer's screen column and row.</summary>
    public event Action<PaneEdge, int, int>? EdgeDragged;

    /// <summary>Raised with the frame line when the overlay box is clicked.</summary>
    public event Action<int>? OverlayLineClicked;

    /// <summary>Raised with the frame line and column when a drag begins on the overlay box.</summary>
    public event Action<int, int>? OverlayDragStarted;

    /// <summary>Raised with the frame line of the pointer while a drag goes on over the overlay box.</summary>
    public event Action<int>? OverlayDragged;

    /// <summary>Raised with the frame line when the overlay box is double-clicked.</summary>
    public event Action<int>? OverlayLineDoubleClicked;

    /// <summary>Raised with the row index when a picker row is clicked (the highlight follows the pointer).</summary>
    public event Action<int>? PickerRowClicked
    {
        add => _picker.RowClicked += value;
        remove => _picker.RowClicked -= value;
    }

    /// <summary>True while panes are shown or hidden: the toolkit moves the focus on its own then, which is not the user's choice.</summary>
    public bool IsChangingVisibility { get; private set; }

    /// <summary>The pane that has focus in the Changes view, from the tab's state; the layout of a narrow screen follows it.</summary>
    public Func<PaneKind> WorkingFocus { get; set; } = () => PaneKind.WorkingUnstaged;

    /// <summary>The keymap view of the diff box (the side of the selected file), for the mouse map.</summary>
    public Func<string> WorkingDiffView { get; set; } = () => DefaultKeymap.WorkingUnstagedDiffView;

    /// <summary>The staging buttons of the diff box's title row: those of the selected file's side.</summary>
    public Func<WorkingTreeButtons> WorkingDiffButtons { get; set; } = () => WorkingTreeButtons.None;

    /// <summary>True while the diff box fills the whole Changes view (<c>&lt;space&gt;z</c>).</summary>
    public Func<bool> IsWorkingDiffZoomed { get; set; } = () => false;

    /// <summary>The title of the diff box of the Changes view: side, hunk and file.</summary>
    public Func<string> WorkingDiffTitle { get; set; } = () => WorkingTreePaneRenderer.DiffTitleName;

    /// <summary>The rows of the Unstaged list of the Changes view.</summary>
    public WorkingTreeListSource UnstagedSource { get; }

    /// <summary>The rows of the Staged list of the Changes view.</summary>
    public WorkingTreeListSource StagedSource { get; }

    /// <summary>The Unstaged file list of the Changes view.</summary>
    public PaneListView WorkingUnstaged { get; }

    /// <summary>The Staged file list of the Changes view.</summary>
    public PaneListView WorkingStaged { get; }

    /// <summary>The diff box of the Changes view.</summary>
    public PaneListView WorkingDiff { get; }

    /// <summary>The editor rows of the commit message box of the Changes view (the option row below them is a separate row).</summary>
    public PaneListView WorkingMessage { get; }

    /// <summary>The commit draft of the active tab, null when it has none; the message box and its option row show it.</summary>
    public Func<CommitDraft?> ActiveDraft { get; set; } = () => null;

    /// <summary>True while the message box is in Insert mode, so it shows the text cursor.</summary>
    public Func<bool> IsEditingMessage { get; set; } = () => false;

    public Window Window { get; }

    public PaneListView Graph { get; }

    public PaneListView Sidebar { get; }

    /// <summary>The commit detail pane below the graph.</summary>
    public PaneListView Detail { get; }

    /// <summary>The diff pane, beside or in place of the detail pane.</summary>
    public PaneListView Diff { get; }

    /// <summary>The floating box for the help, the palette and the which-key popup; added last so it draws on top.</summary>
    public OverlayView Overlay { get; }

    /// <summary>The terminal size in columns and rows.</summary>
    public ScreenSize ScreenSize => new(Window.Viewport.Width, Window.Viewport.Height);

    /// <summary>Makes every pane and the picker swallow mouse input while <paramref name="isSuppressed"/> returns true (an overlay is open).</summary>
    public void SuppressMouseWhen(Func<bool> isSuppressed)
    {
        _suppressed = isSuppressed;
        Graph.IsSuppressed = isSuppressed;
        Sidebar.IsSuppressed = isSuppressed;
        Detail.IsSuppressed = isSuppressed;
        Diff.IsSuppressed = isSuppressed;
        WorkingUnstaged.IsSuppressed = isSuppressed;
        WorkingStaged.IsSuppressed = isSuppressed;
        WorkingDiff.IsSuppressed = isSuppressed;
        WorkingMessage.IsSuppressed = isSuppressed;
        _picker.IsSuppressed = isSuppressed;
    }

    /// <summary>Shows the repository picker in place of the graph, or the graph again.</summary>
    public void ShowPicker(bool isOpen)
    {
        if (_picker.Visible == isOpen)
        {
            return;
        }

        _picker.Visible = isOpen;
        ApplyVisibility();
        if (!isOpen)
        {
            (_isWorkingMode ? FocusedWorkingList() : Graph).SetFocus();
        }

        _picker.SetNeedsDraw();
        Graph.SetNeedsDraw();
    }

    /// <summary>Shows the Changes view in place of the sidebar, graph and detail panes, or those again.</summary>
    public void ShowWorkingTree(bool isShown)
    {
        if (_isWorkingMode == isShown)
        {
            return;
        }

        _isWorkingMode = isShown;
        ApplyVisibility();
        _workingGrid = null;
        _grid = null;
        Window.SetNeedsLayout();
        Window.SetNeedsDraw();
    }

    private int ScreenWidth => Window.Viewport.Width;

    /// <summary>Raised after the chrome was redrawn: the model a popup is built from may have changed.</summary>
    public event Action? ChromeRefreshed;

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

        Sidebar.SetNeedsDraw();
        _detailTitle.SetNeedsDraw();
        _diffTitle.SetNeedsDraw();
        _bottomDividerView.SetNeedsDraw();
        if (_isWorkingMode)
        {
            _workingDivider.SetNeedsDraw();
            _workingLeftEdge.SetNeedsDraw();
            _workingRightEdge.SetNeedsDraw();
            WorkingUnstaged.SetNeedsDraw();
            WorkingStaged.SetNeedsDraw();
            _workingDiffTitle.SetNeedsDraw();
            WorkingDiff.SetNeedsDraw();
            WorkingMessage.SetNeedsDraw();
            _messageOptions.SetNeedsDraw();
            _stageAllButton.SetNeedsDraw();
            _unstageAllButton.SetNeedsDraw();
            _unstagedViewButton.SetNeedsDraw();
            _resolveButton.SetNeedsDraw();
            _stagedViewButton.SetNeedsDraw();
            _unstagedDiffButton.SetNeedsDraw();
            _stagedDiffButton.SetNeedsDraw();
        }
    }

    /// <summary>Repaints every view after the palette changed (the settings screen previews a theme).</summary>
    public void RefreshPalette()
    {
        ApplyPaneSchemes();
        RefreshChrome();
        Window.SetNeedsDraw();
        Graph.SetNeedsDraw();
        Detail.SetNeedsDraw();
        Diff.SetNeedsDraw();
        WorkingUnstaged.SetNeedsDraw();
        WorkingStaged.SetNeedsDraw();
        WorkingDiff.SetNeedsDraw();
        WorkingMessage.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();

    private PaneListView FocusedWorkingList() => WorkingFocus() switch
    {
        PaneKind.WorkingStaged => WorkingStaged,
        PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => WorkingDiff,
        PaneKind.WorkingMessage => WorkingMessage,
        _ => WorkingUnstaged,
    };

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
        var isPicker = _picker.Visible;
        var history = !_isWorkingMode;
        var working = _isWorkingMode;
        _workingDivider.Visible = working;
        _workingLeftEdge.Visible = working;
        _workingRightEdge.Visible = working;
        WorkingUnstaged.Visible = working && !isPicker;
        WorkingStaged.Visible = working && !isPicker;
        WorkingDiff.Visible = working && !isPicker;
        _workingDiffTitle.Visible = working && !isPicker;
        WorkingMessage.Visible = working && !isPicker;
        _messageOptions.Visible = working && !isPicker;
        _stageAllButton.Visible = working && !isPicker;
        _unstageAllButton.Visible = working && !isPicker;
        _unstagedViewButton.Visible = working && !isPicker;
        _resolveButton.Visible = working && !isPicker;
        _stagedViewButton.Visible = working && !isPicker;
        _unstagedDiffButton.Visible = working && !isPicker;
        _stagedDiffButton.Visible = working && !isPicker;
        Sidebar.Visible = history;
        _sidebarFilter.Visible = history;
        _divider.Visible = history;
        _detailTitle.Visible = history;
        Detail.Visible = history;
        _bottomDividerView.Visible = history;
        _diffTitle.Visible = history;
        Diff.Visible = history;
        Graph.Visible = history && !isPicker;
    }

    // Every list pane gets the palette scheme: the toolkit paints the cells no row covers (an empty list, the space below the
    // last row) with the scheme of the view, and without its own it falls back to the toolkit default (#805).
    private void ApplyPaneSchemes()
    {
        foreach (var pane in new View[] { _picker, Graph, Sidebar, Detail, Diff, WorkingUnstaged, WorkingStaged, WorkingDiff, WorkingMessage })
        {
            pane.SetScheme(Palette.BaseScheme());
        }
    }

    private static PaneListView CreateWorkingList(WorkingTreeListSource source, Func<PaneRect> rect) => new()
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        CanFocus = true,
        Source = source,
        Visible = false,
    };

    // The title row of a file list is the top edge of its box, one row above the list; its buttons are the mouse
    // path of stage all / unstage all, the view dropdown and (Unstaged, on a conflicted file) resolve. A button has
    // no width when the title leaves no room for it.
    private LineView CreateListButton(WorkingTreeSection section, PaneListView list, ListButtonKind kind)
    {
        var view = new LineView(_ => ListButtonLine(section, kind))
        {
            X = Pos.Func(_ => ListButtonRect(section, kind).X),
            Y = Pos.Func(_ => ListButtonRect(section, kind).Y),
            Width = Dim.Func(_ => ListButtonRect(section, kind).Width),
            Height = Dim.Func(_ => ListButtonRect(section, kind).Height),
            Visible = false,
        };
        view.Clicked += _ =>
        {
            if (_suppressed() || ListButton(section, kind) is not { } button)
            {
                return;
            }

            FocusFromTitle(list);
            Gestured?.Invoke(button.View, MouseGesture.Click, null);
        };
        return view;
    }

    private TitleButton? ListButton(WorkingTreeSection section, ListButtonKind kind)
    {
        var rect = ListRect(section);
        var tree = section == WorkingTreeSection.Staged ? StagedSource.Tree : UnstagedSource.Tree;
        if (rect.IsEmpty)
        {
            return null;
        }

        var title = WorkingTreePaneRenderer.ListTitle(tree, section);

        // Where the diff is not beside the list (a screen under 80 columns) the diff chip is its mouse path.
        var hasDiffButton = WorkingRects().Diff.IsEmpty;

        return kind switch
        {
            ListButtonKind.View => WorkingListTitleButtons.ViewButton(
                section, title, rect.Width, _model().Keys, hasDiffButton),
            ListButtonKind.Resolve => ResolveButton(title, rect, hasDiffButton),
            ListButtonKind.Diff => DiffButton(section, title, rect, hasDiffButton),
            _ => WorkingListTitleButtons.Layout(section, title, rect.Width, _model().Keys),
        };
    }

    private StyledLine ListButtonLine(WorkingTreeSection section, ListButtonKind kind)
    {
        if (ListButton(section, kind) is { } button)
        {
            return ViewChip.Line(button, _glyphs);
        }

        return new StyledLine([]);
    }

    private TitleButton? ResolveButton(string title, PaneRect rect, bool hasDiffButton)
    {
        if (IsConflictedUnderCursor())
        {
            return WorkingListTitleButtons.ResolveButton(title, rect.Width, _model().Keys, hasDiffButton);
        }

        return null;
    }

    private TitleButton? DiffButton(WorkingTreeSection section, string title, PaneRect rect, bool hasDiffButton)
    {
        if (hasDiffButton)
        {
            return WorkingListTitleButtons.DiffButton(section, title, rect.Width, _model().Keys);
        }

        return null;
    }

    // The resolve chip shows while the Unstaged list's cursor is on a conflicted file (the row <space>gr opens).
    // A file the editor refuses (delete/modify) gets no chip: it would promise what the key then declines.
    private bool IsConflictedUnderCursor() =>
        ConflictedSelection.Find(UnstagedSource.Tree) is { } entry && ConflictEditorSupport.CanOpen(entry.Conflict);

    private PaneRect ListRect(WorkingTreeSection section) =>
        section == WorkingTreeSection.Staged ? WorkingRects().Staged : WorkingRects().Unstaged;

    private PaneRect ListButtonRect(WorkingTreeSection section, ListButtonKind kind)
    {
        var rect = ListRect(section);
        if (ListButton(section, kind) is { } button)
        {
            return new PaneRect(rect.X + button.Start, rect.Y - 1, button.Width, 1);
        }

        return Empty;
    }

    private FrameStripView CreateWorkingEdge(Func<PaneRect> rect) => new(CurrentGrid)
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        Visible = false,
    };

    private void OnMessageClicked(int column, int row)
    {
        var padding = PanePadding.ColumnsFor(ScreenWidth);
        var textWidth = PanePadding.TextWidth(WorkingRects().MessageEditor.Width, padding);
        MessageClicked?.Invoke(column - padding, row, textWidth);
    }

    private WorkingTreeRects WorkingRects() => WorkingTreeLayout.Compute(ScreenWidth, ScreenHeight(), WorkingFocus(), IsWorkingDiffZoomed());

    // The line between the lists and the diff, on the screen's rows of content.
    private PaneRect WorkingDividerRect()
    {
        var diff = WorkingRects().Diff;
        return diff.IsEmpty ? Empty : new PaneRect(diff.X - 1, ScreenLayout.GraphTop, 1, ScreenLayout.GraphHeight(ScreenHeight()));
    }

    // The row where two stacked boxes meet (the lower box's top edge, which carries its title): from the screen's left edge or the divider.
    private static PaneRect WorkingEdgeRect(PaneRect upper, PaneRect lower, bool isLeft)
    {
        if (upper.IsEmpty || lower.IsEmpty)
        {
            return Empty;
        }

        return new PaneRect(isLeft ? 1 : lower.X, lower.Y - 1, lower.Width, 1);
    }

    private int ScreenHeight() => Window.Viewport.Height;

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!_suppressed() && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnDetailTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Detail);
        var selectedTab = (Detail.Source as DetailListSource)?.Content.Tab ?? BottomTab.Changes;
        if (TitleButton.ViewAt(DetailTabStrip.Layout(_detailTitle.Viewport.Width, selectedTab), column) is { } tabView)
        {
            Gestured?.Invoke(tabView, MouseGesture.Click, null);
        }
        else if ((Detail.Source as DetailListSource)?.Content is { } content
            && DetailPaneRenderer.ViewButton(
                content,
                _detailTitle.Viewport.Width,
                _glyphs,
                _model().Keys) is { } chip
            && column >= chip.Start
            && column < chip.Start + chip.Width)
        {
            Gestured?.Invoke(chip.View, MouseGesture.Click, null);
        }
    }

    /// <summary>
    /// Where the <c>view ▾</c> dropdown hangs: under its title button, or under the right end of the title row when
    /// the row is too narrow for the button (the keys still open the menu).
    /// </summary>
    /// <param name="isWorking">The Changes view diff rather than the history diff.</param>
    public MenuAnchor ViewMenuAnchor(bool isWorking)
    {
        var rect = isWorking ? WorkingRects().Diff : Rects().DiffTitle;
        var row = isWorking ? rect.Y - 1 : rect.Y;
        var buttons = DiffTitleButtons.Layout(rect.Width, _model().Keys);
        if (isWorking)
        {
            buttons = DiffTitleButtons.Layout(rect.Width, WorkingDiffButtons(), _model().Keys);
        }

        var button = buttons.FirstOrDefault(candidate => candidate.View == DefaultMouseMap.DiffViewButton);
        var column = button is null ? rect.X + Math.Max(0, rect.Width - 1) : rect.X + button.Start;

        return new MenuAnchor(column - 2, row);
    }

    /// <summary>
    /// Where the <c>view ▾</c> dropdown of a file list hangs: under its title chip, or under the right end of the
    /// title row when the row is too narrow for the chip (the keys still open the menu).
    /// </summary>
    /// <param name="view">The keymap view of the list: the Changes or Files tab, or a Changes view list.</param>
    public MenuAnchor ListViewMenuAnchor(string view)
    {
        if (view == DefaultKeymap.WorkingUnstagedView || view == DefaultKeymap.WorkingStagedView)
        {
            var section = WorkingTreeSection.Unstaged;
            if (view == DefaultKeymap.WorkingStagedView)
            {
                section = WorkingTreeSection.Staged;
            }

            var rect = ListRect(section);
            var chip = ListButton(section, kind: ListButtonKind.View);

            return new MenuAnchor(
                rect.X + (chip?.Start ?? Math.Max(0, rect.Width - 1)) - 2,
                rect.Y - 1);
        }

        var title = Rects().DetailTitle;
        var button = default(TitleButton);
        if ((Detail.Source as DetailListSource)?.Content is { } content)
        {
            button = DetailPaneRenderer.ViewButton(content, title.Width, _glyphs, _model().Keys);
        }

        return new MenuAnchor(title.X + (button?.Start ?? Math.Max(0, title.Width - 1)) - 2, title.Y);
    }

    private void OnDiffTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Diff);
        var buttons = DiffTitleButtons.Layout(
            _diffTitle.Viewport.Width,
            _model().Keys,
            Diff.Source is DiffListSource { Document.IsFileContent: true });
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private void OnWorkingDiffTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(WorkingDiff);
        var buttons = DiffTitleButtons.Layout(_workingDiffTitle.Viewport.Width, WorkingDiffButtons(), _model().Keys);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private StyledLine WorkingDiffTitleLine(int width)
    {
        var rect = WorkingRects().Diff;
        var line = DiffPaneRenderer.RenderWorkingTitle(
            WorkingDiffTitle(),
            WorkingPanes.IsDiff(WorkingFocus()),
            WorkingDiffButtons(),
            width,
            _glyphs,
            _model().Keys);

        return CurrentGrid().ResolveRules(line, rect.Y - 1, rect.X);
    }

    // The frame's bottom edge, or the in-progress operation banner over it (ux-workflows.md, workflow 4).
    private StyledLine BottomEdgeLine(int width)
    {
        var edge = CurrentGrid().Row(ScreenLayout.FrameBottom(ScreenHeight()), 0, width);
        return BannerRenderer.EdgeLine(_model(), edge, width, ScreenHeight(), _glyphs);
    }

    private void OnBottomEdgeClicked(int column)
    {
        var model = _model();
        if (_suppressed() || model.Banner is not { } banner || ScreenHeight() < ScreenLayout.MinimumHeightWithBanner)
        {
            return;
        }

        if (BannerRenderer.Build(banner, ScreenWidth, _glyphs).ViewAt(column) is { } view)
        {
            Gestured?.Invoke(view, MouseGesture.Click, null);
        }
    }

    private FrameGrid CurrentGrid() => _isWorkingMode && !_picker.Visible ? CurrentWorkingGrid() : CurrentHistoryGrid();

    private FrameGrid CurrentWorkingGrid()
    {
        var focus = WorkingFocus();
        var rects = WorkingRects();
        var unstagedTitle = WorkingTreePaneRenderer.ListTitle(UnstagedSource.Tree, WorkingTreeSection.Unstaged);
        var stagedTitle = WorkingTreePaneRenderer.ListTitle(StagedSource.Tree, WorkingTreeSection.Staged);
        var messageTitle = ActiveDraft()?.Title ?? WorkingTreePaneRenderer.MessageTitle;
        var key = new WorkingGridKey(
            ScreenWidth,
            ScreenHeight(),
            rects,
            focus,
            unstagedTitle,
            stagedTitle,
            messageTitle,
            _glyphs.IsAscii);
        if (_workingGrid is null || _workingGridKey != key)
        {
            _workingGrid = FrameGrid.Build(
                ScreenWidth,
                ScreenHeight(),
                [
                    new FrameBox(rects.Unstaged, focus == PaneKind.WorkingUnstaged, unstagedTitle),
                    new FrameBox(rects.Staged, focus == PaneKind.WorkingStaged, stagedTitle),
                    new FrameBox(rects.Diff, WorkingPanes.IsDiff(focus), string.Empty),
                    new FrameBox(rects.Message, focus == PaneKind.WorkingMessage, messageTitle),
                ],
                _glyphs);
            _workingGridKey = key;
        }

        return _workingGrid;
    }

    private FrameGrid CurrentHistoryGrid()
    {
        var isPicker = _picker.Visible;
        var rects = isPicker ? PickerRects() : Rects();
        var focused = FocusedHistoryPane(isPicker);
        var title = _model().RepositoryName.Length > 0 ? _model().RepositoryName : "Repository";
        var key = new GridKey(
            ScreenWidth,
            ScreenHeight(),
            rects,
            focused,
            title,
            isPicker,
            _glyphs.IsAscii,
            _model().RepeatPane);
        if (_grid is null || _gridKey != key)
        {
            var sidebarTitle = isPicker ? string.Empty : RepeatMode.Mark(title, key.RepeatPane == PaneKind.Sidebar);
            var graphTitle = isPicker ? string.Empty : RepeatMode.Mark("History", key.RepeatPane == PaneKind.Graph);
            _grid = FrameGrid.Build(ScreenWidth, ScreenHeight(), rects, focused, _glyphs, sidebarTitle, graphTitle);
            _gridKey = key;
        }

        return _grid;
    }

    private PaneRects PickerRects()
    {
        var graph = new PaneRect(
            1,
            ScreenLayout.GraphTop,
            ScreenLayout.InnerWidth(ScreenWidth),
            ScreenLayout.GraphHeight(ScreenHeight()));

        return new PaneRects(Empty, Empty, Empty, graph, Empty, Empty, Empty, Empty, Empty);
    }

    private PaneKind FocusedHistoryPane(bool isPicker)
    {
        if (isPicker)
        {
            return PaneKind.Graph;
        }

        if (Sidebar.HasFocus)
        {
            return PaneKind.Sidebar;
        }

        if (Detail.HasFocus)
        {
            return PaneKind.Detail;
        }

        return Diff.HasFocus ? PaneKind.Diff : PaneKind.Graph;
    }

    private readonly record struct WorkingGridKey(
        int Width,
        int Height,
        WorkingTreeRects Rects,
        PaneKind Focus,
        string UnstagedTitle,
        string StagedTitle,
        string MessageTitle,
        bool IsAscii);

    private StyledLine OptionsLine(int width) =>
        MessageBoxRenderer.OptionsLine(
            ActiveDraft(),
            width,
            PanePadding.ColumnsFor(ScreenWidth),
            _glyphs,
            _model().Keys);

    // The checkboxes and the Commit button of the option row are the mouse path of the Alt- chords and the commit key.
    private void OnMessageOptionsClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        var padding = PanePadding.ColumnsFor(ScreenWidth);
        var textWidth = PanePadding.TextWidth(_messageOptions.Viewport.Width, padding);
        var buttons = MessageBoxRenderer.Buttons(textWidth, padding, _model().Keys);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private PaneRects Rects()
    {
        var current = _arrangement();
        return PaneLayout.Compute(
            ScreenWidth,
            ScreenHeight(),
            current.IsDiffOpen,
            current.Zoomed,
            current.Sizes,
            current.ZoomBoth);
    }
}
