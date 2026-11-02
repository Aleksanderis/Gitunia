using Gitunia.App;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Screen;

/// <summary>What the main view draws: the screen model and the title rows of the panes.</summary>
public sealed class ScreenContent : IScreenContent
{
    private readonly Dispatcher _host;
    private readonly ScreenModelSource _model;
    private readonly IActiveSessions _sessions;
    private readonly PaneViews _panes;
    private readonly PaneTitleParts _titles;

    /// <param name="host">The state.</param>
    /// <param name="model">Builds the screen model.</param>
    /// <param name="sessions">The shown tab's detail rows.</param>
    /// <param name="panes">The lists, whose focus the titles show.</param>
    /// <param name="titles">The repeat mode and the diff controllers that know their titles.</param>
    public ScreenContent(
        Dispatcher host,
        ScreenModelSource model,
        IActiveSessions sessions,
        PaneViews panes,
        PaneTitleParts titles)
    {
        _host = host;
        _model = model;
        _sessions = sessions;
        _panes = panes;
        _titles = titles;
    }

    /// <inheritdoc />
    public ScreenModel Model() => _model.Model();

    /// <inheritdoc />
    public StyledLine DetailTitle(int width)
    {
        var content = _sessions.ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;

        return DetailPaneRenderer.RenderTitle(
            content,
            _panes.Detail.HasFocus,
            width,
            _panes.Content.Glyphs,
            _panes.Content.Keys,
            _titles.Repeat.Active?.Pane == PaneKind.Detail);
    }

    /// <inheritdoc />
    public StyledLine DiffTitle(int width) =>
        _titles.DiffView.TitleLine(
            width,
            _panes.Content.Glyphs,
            _panes.Content.Keys,
            _titles.Repeat.Active?.Pane == PaneKind.Diff);

    /// <inheritdoc />
    public SidebarChrome SidebarFilter() => SidebarChrome.Of(_host.State);

    /// <inheritdoc />
    public string WorkingDiffTitle() =>
        _titles.WorkingDiff.TitleText(_titles.Repeat.Active?.Pane is PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff);
}
