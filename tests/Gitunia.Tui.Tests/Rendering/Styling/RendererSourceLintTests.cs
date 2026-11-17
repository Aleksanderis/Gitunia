using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Styling;

/// <summary>Source-level guards of the colour roles (#1449): key labels only on keys, no literal glyphs outside the glyph tables.</summary>
public partial class RendererSourceLintTests
{
    private const int KeyContextLines = 6;

    // The files that draw key labels; the rest of the renderers go through them.
    private static readonly string[] KeyLabelFiles =
    [
        "ActionRow.cs",
        "ChipStyle.cs",
        "ContextMenuFrameBuilder.cs",
        "HelpFrameBuilder.cs",
        "HintBar.cs",
        "PaletteFrameBuilder.cs",
        "SettingsFrameBuilder.cs",
        "WhichKeyFrameBuilder.cs",
    ];

    // The files that hold the glyph tables.
    private static readonly string[] GlyphTableFiles =
    [
        "GraphGlyphSet.cs",
        "GlyphLiterals.cs",
        "PickerGlyphs.cs",
        "SidebarGlyphs.cs",
    ];

    [Fact]
    public void GivenTheRenderers_WhenSearchingForKeyLabels_ThenOnlyKeySegmentsUseThem()
    {
        var failures = new List<string>();
        foreach (var file in RendererFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!KeyLabelRole().IsMatch(lines[index]) || lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = Path.GetFileName(file);
                var from = Math.Max(0, index - KeyContextLines);
                var context = string.Join('\n', lines[from..(index + 1)]);
                if (!KeyLabelFiles.Contains(name) || !context.Contains("key", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{name}:{index + 1}");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void GivenTheRenderers_WhenSearchingForGlyphLiterals_ThenOnlyTheGlyphTablesHoldThem()
    {
        var failures = new List<string>();
        foreach (var file in RendererFiles().Where(file => !GlyphTableFiles.Contains(Path.GetFileName(file))))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var code = lines[index].Split("//")[0];
                if (GlyphLiteral().IsMatch(code))
                {
                    failures.Add($"{Path.GetFileName(file)}:{index + 1}");
                }
            }
        }

        Assert.Empty(failures);
    }

    private static IEnumerable<string> RendererFiles() =>
        Directory.EnumerateFiles(Path.Combine(SourceRoot(), "src", "Gitunia.Tui", "Rendering"), "*.cs", SearchOption.AllDirectories);

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex(@"SegmentRole\.(Bar)?KeyLabel")]
    private static partial Regex KeyLabelRole();

    [GeneratedRegex("[─│┌┐└┘├┤┬┴┼╭╮╯╰╲╱→←↑↓✓✗✔✘✕▶▼►◀▲●○◉⎇▸▾⏎]")]
    private static partial Regex GlyphLiteral();
}
