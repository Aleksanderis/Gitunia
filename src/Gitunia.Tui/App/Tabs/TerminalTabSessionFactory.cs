using Gitunia.App.Tabs;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Tabs;

/// <summary>Builds the sessions with their terminal side, drawn with the current glyph set.</summary>
/// <param name="glyphs">The glyph set the next sessions draw with.</param>
/// <param name="workingButtons">The staging buttons of the Changes view diff; none when null.</param>
public sealed class TerminalTabSessionFactory(GraphGlyphSet glyphs, IGroupButtonSource? workingButtons = null) : ITabSessionFactory<TerminalTabSession>
{
    /// <summary>The glyph set new sessions take.</summary>
    public GraphGlyphSet Glyphs { get; set; } = glyphs;

    public TerminalTabSession Create(TabSessionParts parts) => new(parts, Glyphs, workingButtons);
}
