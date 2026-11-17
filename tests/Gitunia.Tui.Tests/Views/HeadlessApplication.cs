using Gitunia.Tui.App.Shell;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace Gitunia.Tui.Tests.Views;

/// <summary>
/// A Terminal.Gui application on the headless ANSI driver, disposed the way the product does it (#1369: a plain
/// <c>Dispose</c> can wait for ever on Windows for an input thread that is stuck in a console read).
/// </summary>
public sealed class HeadlessApplication : IDisposable
{
    private readonly ApplicationDisposer _disposer = new();

    public HeadlessApplication()
    {
        Application = Terminal.Gui.App.Application.Create();
        Application.Init(DriverRegistry.Names.ANSI);
    }

    public IApplication Application { get; }

    public void Dispose() => _disposer.Dispose(Application);
}
