using Gitunia.Tui.Rendering.Diff;
using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.Tests.Views;

/// <summary>The main view with its panes and an empty screen model, as the application composes them.</summary>
public sealed class MainViewFixture : IDisposable
{
    public MainViewFixture()
    {
        var chrome = new PaneChrome();
        var gate = new MouseGate();
        var geometry = new PaneGeometry(chrome, new FixedArrangement(), new FixedWorkingFocus());
        Panes = new PaneViews(
            chrome,
            geometry,
            gate,
            new PaneViewContent(
                GraphGlyphSet.Unicode,
                new ChipKeys(DefaultKeymap.Create()),
                new SidebarListSource(GraphGlyphSet.Unicode),
                new StateMessageBoxPort(new Dispatcher(AppState.Empty))));
        var mode = new ScreenMode();
        var content = new EmptyContent();
        var grids = new FrameGrids(Panes, geometry, mode, content);
        var parts = new ChromeParts(Panes, geometry, grids, content, gate, mode);
        View = new MainView(parts, new WorkingTreeChrome(parts, new WorkingListButtons(geometry, Panes)));
    }

    public PaneViews Panes { get; }

    public MainView View { get; }

    public void Dispose() => View.Dispose();

    private sealed class FixedArrangement : IPaneArrangement
    {
        public PaneArrangement Arrangement() => PaneArrangement.Normal;
    }

    private sealed class FixedWorkingFocus : IWorkingTreeFocus
    {
        public PaneKind FocusedPane() => PaneKind.WorkingUnstaged;

        public string DiffView() => "WorkingUnstagedDiff";

        public bool IsDiffZoomed() => false;

        public WorkingTreeButtons DiffButtons() => WorkingTreeButtons.None;
    }

    private sealed class EmptyContent : IScreenContent
    {
        private static readonly ScreenModel EmptyModel =
            new("repository", null, 1, 1, "Normal", string.Empty, 0, 0, false, string.Empty);

        public ScreenModel Model() => EmptyModel;

        public StyledLine DetailTitle(int width) => new([]);

        public StyledLine DiffTitle(int width) => new([]);

        public SidebarChrome SidebarFilter() => new(string.Empty, false, false);

        public string WorkingDiffTitle() => WorkingTreePaneRenderer.DiffTitleName;
    }
}
