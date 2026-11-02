using Gitunia.Tui.Views;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>A screen of a given size that counts the redraws and layouts the panes asked for.</summary>
/// <param name="screenSize">The terminal size.</param>
public sealed class FakePaneChrome(ScreenSize screenSize) : IPaneChrome
{
    public ScreenSize ScreenSize { get; set; } = screenSize;

    public int Relayouts { get; private set; }

    public int Refreshes { get; private set; }

    public void Relayout() => Relayouts++;

    public void RefreshChrome() => Refreshes++;
}
