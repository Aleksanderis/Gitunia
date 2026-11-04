using Gitunia.App;
using Gitunia.Core.Visual;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>
/// Copy on select (<c>copyOnSelect</c>): a selection the pointer finished (a drag released, a double or triple click) in
/// a pane or in the blame view is copied at once. The selection stays, so <c>y</c> or <c>Ctrl-c</c> copies it again.
/// </summary>
public sealed class CopyOnSelect
{
    private readonly Dispatcher _host;
    private readonly BlameInput _blame;
    private readonly LiveConfig _config;

    /// <param name="host">Takes the copy request.</param>
    /// <param name="visual">Tells when the pointer finished a selection in a pane.</param>
    /// <param name="blame">Tells when the pointer finished a selection in the blame view.</param>
    /// <param name="config">Says whether copy on select is on.</param>
    public CopyOnSelect(Dispatcher host, VisualRangeInput visual, BlameInput blame, LiveConfig config)
    {
        _host = host;
        _blame = blame;
        _config = config;
        visual.PointerSelected += OnPaneSelected;
        blame.PointerSelected += OnBlameSelected;
    }

    private void OnPaneSelected()
    {
        if (_config.Options.CopyOnSelect)
        {
            _host.Dispatch(new VisualCopyRequested(VisualCopyMode.OnSelect));
        }
    }

    private void OnBlameSelected()
    {
        if (_config.Options.CopyOnSelect)
        {
            _blame.CopySelection(VisualCopyMode.OnSelect);
        }
    }
}
