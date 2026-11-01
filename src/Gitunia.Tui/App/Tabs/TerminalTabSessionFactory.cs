using Gitunia.App.Tabs;
using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.App.Tabs;

/// <summary>Builds the sessions with their terminal side, drawn with the current glyph set.</summary>
/// <param name="glyphs">The glyph set the next sessions draw with.</param>
public sealed class TerminalTabSessionFactory(GraphGlyphSet glyphs) : ITabSessionFactory<TerminalTabSession>
{
    /// <summary>The glyph set new sessions take.</summary>
    public GraphGlyphSet Glyphs { get; set; } = glyphs;

    public TerminalTabSession Create(TabSessionParts parts) => new(parts, Glyphs);
}
