using System.Collections.Immutable;
using Gitunia.App.Tabs;
using Gitunia.Core.Config.Models;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>Makes the options and the theme of the settings screen take effect in the running app (ux-settings.md, "Settings screen").</summary>
/// <param name="config">The configuration the app follows.</param>
/// <param name="targets">What a change reaches: the overlays, the picker, the terminal and the main view.</param>
/// <param name="factory">Builds the sessions of new tabs with the current glyphs.</param>
/// <param name="registry">The open tabs, whose graphs are redrawn with new glyphs.</param>
/// <param name="panes">The graph list and the screen to lay out again.</param>
public sealed class OptionsApplier(
    LiveConfig config,
    OptionTargets targets,
    TerminalTabSessionFactory factory,
    TabSessionRegistry<TerminalTabSession> registry,
    PaneViews panes)
{
    /// <summary>The settings screen changed the file: the running app takes the new options at once.</summary>
    /// <param name="options">The options now in the file.</param>
    public void ApplyOptions(ConfigOptions options)
    {
        config.ChangeOptions(options);
        ApplyLabelCaps(options.LabelCaps);
        targets.Overlays.OnOptionsChanged();
        if (!SameRoots(targets.Picker.ScanRoots, options.ScanRoots))
        {
            targets.Picker.ChangeScanRoots(options.ScanRoots);
        }

        panes.Chrome.Relayout();
    }

    /// <summary>
    /// The theme on screen changed (previewed, kept or restored by the settings screen): the palette is rebuilt and every
    /// view repainted. An auto theme that was not asked for at start uses the COLORFGBG guess; the terminal is only
    /// queried (OSC 11) before the UI takes over the input.
    /// </summary>
    /// <param name="theme">The theme to show.</param>
    public void ApplyTheme(ThemeChoice theme)
    {
        if (config.Theme == theme)
        {
            return;
        }

        config.ChangeTheme(theme);
        targets.Terminal.ConfigurePalette();
        targets.View.RefreshPalette();
    }

    // The caps change the graph labels only: every tab's graph is redrawn with the set that has (or lacks) them.
    private void ApplyLabelCaps(LabelCapStyle style)
    {
        if (!config.ChangeLabelCaps(style))
        {
            return;
        }

        factory.Glyphs = config.Glyphs;
        foreach (var session in registry.Values)
        {
            session.Source.Glyphs = config.Glyphs;
        }

        panes.Graph.SetNeedsDraw();
    }

    private static bool SameRoots(ImmutableArray<string>? current, ImmutableArray<string>? other)
    {
        if (current is null || other is null)
        {
            return current is null && other is null;
        }

        return current.Value.SequenceEqual(other.Value);
    }
}
