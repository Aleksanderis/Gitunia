using Gitunia.Tui.Rendering.Diff;
using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Operations;
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
    /// <param name="banner">An operation banner to show, null for none; the window is then given a fixed size so the
    /// banner row has a place on screen.</param>
    public MainViewFixture(OperationBanner? banner = null)
    {
        var chrome = new PaneChrome();
        Gate = new MouseGate();
        var gate = Gate;
        var geometry = new PaneGeometry(chrome, new FixedArrangement(banner is not null), new FixedWorkingFocus());
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
        var content = new EmptyContent(banner);
        var grids = new FrameGrids(Panes, geometry, mode, content);
        var parts = new ChromeParts(Panes, geometry, grids, content, gate, mode);
        Working = new WorkingTreeChrome(parts, new WorkingListButtons(geometry, Panes));
        View = new MainView(parts, Working);
        if (banner is not null)
        {
            chrome.Window.Width = BannerScreenWidth;
            chrome.Window.Height = BannerScreenHeight;
            chrome.Window.Layout();
        }
    }

    /// <summary>The columns of the window of a fixture with a banner.</summary>
    public const int BannerScreenWidth = 100;

    /// <summary>The rows of the window of a fixture with a banner.</summary>
    public const int BannerScreenHeight = 24;

    public PaneViews Panes { get; }

    /// <summary>The gate that suppresses the panes while an overlay is open.</summary>
    public MouseGate Gate { get; }

    public WorkingTreeChrome Working { get; }

    public MainView View { get; }

    public void Dispose() => View.Dispose();

    private sealed class FixedArrangement(bool hasBanner) : IPaneArrangement
    {
        public PaneArrangement Arrangement() => PaneArrangement.Normal;

        public bool HasBanner() => hasBanner;
    }

    private sealed class FixedWorkingFocus : IWorkingTreeFocus
    {
        public PaneKind FocusedPane() => PaneKind.WorkingUnstaged;

        public string DiffView() => "WorkingUnstagedDiff";

        public bool IsDiffZoomed() => false;

        public int? ListWidth() => null;

        public WorkingTreeButtons DiffButtons() => WorkingTreeButtons.None;
    }

    private sealed class EmptyContent(OperationBanner? banner) : IScreenContent
    {
        private readonly ScreenModel _model =
            new("repository", null, 1, 1, "Normal", string.Empty, 0, 0, false, string.Empty, Banner: banner);

        public ScreenModel Model() => _model;

        public StyledLine DetailTitle(int width) => new([]);

        public StyledLine DiffTitle(int width) => new([]);

        public SidebarChrome SidebarFilter() => new(string.Empty, false, false);

        public string WorkingDiffTitle() => WorkingTreePaneRenderer.DiffTitleName;
    }
}
