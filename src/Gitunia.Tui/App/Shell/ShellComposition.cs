using Gitunia.App;
using Gitunia.Platform;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The composition root: builds every object of the shell once, stage by stage, each from the stages before it. No object
/// is wired up afterwards; <see cref="ShellWiring"/> only subscribes them to each other's events.
/// </summary>
public sealed class ShellComposition : IDisposable
{
    /// <param name="services">The git readers and writers.</param>
    /// <param name="platform">The platform's directories and terminal queries.</param>
    /// <param name="capabilities">What the terminal can show.</param>
    /// <param name="launch">How the shell was asked to start.</param>
    public ShellComposition(GitServices services, IPlatform platform, ConsoleCapabilities capabilities, TuiLaunch launch)
    {
        Foundation = new ShellFoundation(services, platform, capabilities, launch);
        Route = new RouteParts(Foundation);
        Panes = new PaneParts(Foundation, Route);
        Overlays = new OverlayParts(Foundation, Route, Panes);
        Keys = new KeyParts(Foundation, Route, Panes, Overlays);
        Handlers = new HandlerParts(Foundation, Route, Panes, Overlays, Keys);
        Screen = new ScreenParts(Foundation, Panes, Overlays, Keys, Handlers);
        Effects = new EffectParts(Foundation, Panes, Keys, Handlers, Screen);
    }

    /// <summary>The configuration, the terminal, the state and the widgets.</summary>
    public ShellFoundation Foundation { get; }

    /// <summary>Turns a key into an action.</summary>
    public RouteParts Route { get; }

    /// <summary>The controllers of the panes.</summary>
    public PaneParts Panes { get; }

    /// <summary>The prompts, editors and screens.</summary>
    public OverlayParts Overlays { get; }

    /// <summary>The key and mouse controllers.</summary>
    public KeyParts Keys { get; }

    /// <summary>The action handlers.</summary>
    public HandlerParts Handlers { get; }

    /// <summary>The main view and the views that follow the state.</summary>
    public ScreenParts Screen { get; }

    /// <summary>The effect runner, the session saving and the pollers.</summary>
    public EffectParts Effects { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        Effects.Dispose();
        Route.WhichKeyTimer.Dispose();
        Screen.View.Dispose();
    }
}
