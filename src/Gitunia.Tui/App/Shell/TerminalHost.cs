using Gitunia.Platform;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.Input;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The Terminal.Gui application and its life: creating and initialising it, running the main loop, stopping it, handing the
/// terminal to an external tool and starting the application again afterwards. The rest of the program never touches the
/// toolkit's application object, which is replaced after every hand-off.
/// </summary>
public sealed class TerminalHost : ITerminalSuspender, IUiDeferral, IDisposable
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency (docs/internal/history/decisions.md);
    // the idle rate saves CPU (#909) and the per-iteration cursor home + hide writes Terminal.Gui emits (#1207, #1209).
    // 5/s is the lowest rate where writes stop falling; the first key after idle costs about +70 ms median (+90 ms worst) (102 ms
    // against 31 ms at 50/s); 2/s writes no less and costs 204 ms (docs/internal/performance.md, "Idle output").
    private const ushort ActiveIterationsPerSecond = 100;
    private const ushort IdleIterationsPerSecond = 5;
    private const int HeadlessColumns = 120;
    private const int HeadlessRows = 40;
    private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(2);

    private readonly string? _driverName;
    private readonly LiveConfig _config;
    private readonly TerminalHandOff _handOff;
    private readonly IterationRateGovernor _iterationRate = new(ActiveIterationsPerSecond, IdleIterationsPerSecond, IdleAfter);
    private IApplication _application;
    private Action? _suspendedWork;
    private Action? _resumed;
    private bool _isApplicationDisposed;

    /// <param name="driverName">The Terminal.Gui driver to force (smoke tests); null picks the platform default.</param>
    /// <param name="config">The theme and the terminal's capabilities the palette is chosen from.</param>
    /// <param name="handOff">Gives the terminal to a tool and takes it back.</param>
    public TerminalHost(string? driverName, LiveConfig config, TerminalHandOff handOff)
    {
        _driverName = driverName;
        _config = config;
        _handOff = handOff;
        Application.MaximumIterationsPerSecond = ActiveIterationsPerSecond;
        _application = CreateApplication();
        Invoker = new SuspendableUiInvoker(_application.Invoke);
        SubscribeInput();
    }

    /// <summary>Raised for every key press; a subscriber that takes the key sets <c>Handled</c>.</summary>
    public event EventHandler<Key>? KeyDown;

    /// <summary>Raised for a bracketed paste; a subscriber that takes it sets <c>Handled</c>.</summary>
    public event EventHandler<PasteEventArgs>? Pasted;

    /// <summary>Raised on the UI thread when the terminal window gained focus.</summary>
    public event Action? FocusGained;

    /// <summary>Moves work to the UI thread; pauses while a tool owns the terminal.</summary>
    public SuspendableUiInvoker Invoker { get; }

    /// <inheritdoc />
    public void DeferToNextIteration(Action work) => _application.AddTimeout(
        TimeSpan.Zero,
        () =>
        {
            work();

            return false;
        });

    /// <summary>Runs <paramref name="work"/> once on the UI thread after <paramref name="delay"/>, if the loop is still running then.</summary>
    /// <param name="delay">The wait.</param>
    /// <param name="work">What to run.</param>
    public void RunAfter(TimeSpan delay, Action work) => _application.AddTimeout(
        delay,
        () =>
        {
            work();

            return false;
        });

    /// <summary>Stops the main loop.</summary>
    public void RequestStop() => _application.RequestStop();

    /// <summary>
    /// Stops the main loop; <see cref="Run"/> then shuts the driver down (the terminal is back in its normal mode, the
    /// input thread is gone), runs <paramref name="work"/> and starts the driver again, which redraws at the current size.
    /// </summary>
    /// <param name="work">Runs with the terminal in its normal mode.</param>
    /// <param name="resumed">Runs on the UI thread once the application is back.</param>
    public void Suspend(Action work, Action resumed)
    {
        _suspendedWork = work;
        _resumed = resumed;
        Invoker.Suspend();
        _application.RequestStop();
    }

    /// <summary>Builds the palette for the theme on screen.</summary>
    public void ConfigurePalette()
    {
        var driver = _application.Driver;
        var palette = PaletteSettings
            .Choose(_config.Capabilities, driver is { SupportsTrueColor: true, Force16Colors: false })
            .WithTheme(_config.Theme, _config.Startup.Themes);
        if (palette.Depth == ColorDepth.Ansi16 && driver is not null)
        {
            driver.Force16Colors = true;
        }

        Palette.Configure(palette);
    }

    /// <summary>Starts watching for the terminal window gaining focus.</summary>
    public void StartFocusReporting()
    {
        if (_application.Driver is { } driver)
        {
            TerminalFocusReporting.Start(driver, () => Invoker.Invoke(() => FocusGained?.Invoke()));
        }
    }

    /// <summary>Runs the window until the user quits; a hand-off to a tool stops the loop and starts it again.</summary>
    /// <param name="window">The window to show.</param>
    public void Run(Window window)
    {
        try
        {
            _application.Run(window);
            while (_suspendedWork is not null)
            {
                RunSuspended();
                _application.Run(window);
            }
        }
        finally
        {
            Invoker.Close();
            StopFocusReporting();
            _isApplicationDisposed = true;
            _application.Dispose();
        }
    }

    /// <summary>Draws one frame inside the first loop iteration, hands the screen text to the callback and stops the loop.</summary>
    /// <param name="captured">Receives the screen text; null for none.</param>
    public void StopAfterFirstFrame(Action<string>? captured) => _application.Invoke(() => CaptureFirstFrameAndStop(captured));

    /// <summary>Disposes the application when <see cref="Run"/> never started or did not get to its end; a no-op after a run.</summary>
    public void Dispose()
    {
        if (_isApplicationDisposed)
        {
            return;
        }

        _isApplicationDisposed = true;
        Invoker.Close();
        StopFocusReporting();
        _application.Dispose();
    }

    private void RunSuspended()
    {
        var work = _suspendedWork;
        var resumed = _resumed;
        _suspendedWork = null;
        _resumed = null;
        StopFocusReporting();
        _application.Dispose();
        try
        {
            _handOff.Run(work ?? DoNothing);
        }
        finally
        {
            _application = CreateApplication();
            SubscribeInput();
            StartFocusReporting();
            Invoker.Resume(_application.Invoke);
            if (resumed is not null)
            {
                _application.Invoke(resumed);
            }
        }
    }

    private void CaptureFirstFrameAndStop(Action<string>? captured)
    {
        EnsureHeadlessScreenSize();
        _application.LayoutAndDraw(true);
        captured?.Invoke(_application.Driver?.ToString() ?? string.Empty);
        _application.RequestStop();
    }

    // The headless ANSI driver learns its size from a background poll and reports 0x0 until that lands, which under
    // load is after the first iteration (an empty frame, #628). A real terminal has its size from Init.
    private void EnsureHeadlessScreenSize()
    {
        if (_application.Driver is { Cols: 0 } or { Rows: 0 })
        {
            _application.Driver.SetScreenSize(HeadlessColumns, HeadlessRows);
        }
    }

    // The same setup for the first start and for every restart after a hand-off to an external tool.
    private IApplication CreateApplication()
    {
        var application = Application.Create();
        application.Init(_driverName);
        _application = application;
        ConfigurePalette();

        return application;
    }

    private void StopFocusReporting()
    {
        if (_application.Driver is { } driver)
        {
            TerminalFocusReporting.Stop(driver);
        }
    }

    private void SubscribeInput()
    {
        _application.Keyboard.KeyDown += (_, key) =>
        {
            _iterationRate.NoteInput();
            KeyDown?.Invoke(this, key);
        };
        _application.Paste += (_, paste) =>
        {
            _iterationRate.NoteInput();
            Pasted?.Invoke(this, paste);
        };
        _application.Mouse.MouseEvent += (_, _) => _iterationRate.NoteInput();
        _application.Iteration += (_, _) => Application.MaximumIterationsPerSecond = _iterationRate.CurrentRate();
    }

    private static void DoNothing()
    {
    }
}
