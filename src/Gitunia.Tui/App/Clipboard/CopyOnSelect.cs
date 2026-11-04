using Gitunia.App;
using Gitunia.Core.Visual;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>
/// Copy on select (<c>copyOnSelect</c>): a selection the pointer finished (a drag released, a double or triple click)
/// is copied at once. The selection stays, so <c>y</c> or <c>Ctrl-c</c> copies it again.
/// </summary>
public sealed class CopyOnSelect
{
    private readonly Dispatcher _host;
    private readonly LiveConfig _config;

    /// <param name="host">Takes the copy request.</param>
    /// <param name="visual">Tells when the pointer finished a selection.</param>
    /// <param name="config">Says whether copy on select is on.</param>
    public CopyOnSelect(Dispatcher host, VisualRangeInput visual, LiveConfig config)
    {
        _host = host;
        _config = config;
        visual.PointerSelected += OnPointerSelected;
    }

    private void OnPointerSelected()
    {
        if (_config.Options.CopyOnSelect)
        {
            _host.Dispatch(new VisualCopyRequested(VisualCopyMode.OnSelect));
        }
    }
}
