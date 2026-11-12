using Gitunia.Core.Config.Models;
using Gitunia.Platform;
using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.App.Config;

/// <summary>
/// The configuration the running app follows: what the config file said at startup, changed afterwards by the settings
/// screen (options, theme) and by the label caps and the graph line style, plus what the terminal can show. Readers ask it on each use, so a
/// change shows at once.
/// </summary>
/// <param name="startup">What the config file said at startup.</param>
/// <param name="capabilities">What the terminal can show.</param>
/// <param name="homeDirectory">What <c>~</c> stands for in a scan root.</param>
public sealed class LiveConfig(StartupConfig startup, ConsoleCapabilities capabilities, string homeDirectory)
{
    /// <summary>What the config file said at startup, with the later changes of options and theme.</summary>
    public StartupConfig Startup { get; private set; } = startup;

    /// <summary>What the terminal can show.</summary>
    public ConsoleCapabilities Capabilities { get; } = capabilities;

    /// <summary>What <c>~</c> stands for in a scan root.</summary>
    public string HomeDirectory { get; } = homeDirectory;

    /// <summary>The characters the graph and the overlays are drawn with, with the label caps of the options.</summary>
    public GraphGlyphSet Glyphs { get; private set; } = GlyphsFor(startup.Options, capabilities);

    /// <summary>The options of the config file.</summary>
    public ConfigOptions Options => Startup.Options;

    /// <summary>The theme on screen.</summary>
    public ThemeChoice Theme => Startup.Theme;

    /// <summary>The keymap with the config file applied.</summary>
    public KeymapBuildResult Keymap => Startup.Keymap;

    /// <summary>Takes new options.</summary>
    /// <param name="options">The options the settings screen saved.</param>
    public void ChangeOptions(ConfigOptions options) => Startup = Startup with { Options = options };

    /// <summary>Takes a new theme.</summary>
    /// <param name="theme">The theme the settings screen shows.</param>
    public void ChangeTheme(ThemeChoice theme) => Startup = Startup with { Theme = theme };

    /// <summary>Takes the glyph set of the label caps and the graph line style of new options.</summary>
    /// <param name="options">The options the settings screen saved.</param>
    /// <returns>True when the glyph set changed.</returns>
    public bool ChangeGlyphs(ConfigOptions options)
    {
        var glyphs = GlyphsFor(options, Capabilities);
        if (glyphs == Glyphs)
        {
            return false;
        }

        Glyphs = glyphs;

        return true;
    }

    private static GraphGlyphSet GlyphsFor(ConfigOptions options, ConsoleCapabilities capabilities) =>
        GraphGlyphSet.For(capabilities.Unicode)
            .WithLaneStyle(options.LaneStyle)
            .WithLabelCaps(options.LabelCaps, capabilities.Color != ColorDepth.None);
}
