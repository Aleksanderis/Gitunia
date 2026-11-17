using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Core.Tests.Conventions;

/// <summary>
/// Source-scanning guards for one wording and one value format (docs/internal/ui-consistency.md, sections c2 and e):
/// the no-match, nothing, unchanged and loading texts live in <c>UiTexts</c>, the hash length in <c>ShortHash</c>, and
/// counts go through <c>CountText.Plural</c>.
/// </summary>
public sealed partial class UiWordingLintTests
{
    private static readonly string[] SourceProjects = ["Gitunia.Core", "Gitunia.App", "Gitunia.Tui"];

    // Files where "== 1 ?" is not a plural: a pick of the only candidate, a git hunk header, and verb agreement.
    private static readonly string[] SingularChecks =
    [
        "BranchResolver.cs", "PathCompleter.cs", "SyncRemoteDelete.cs", "NewFilePatch.cs", "ResetTexts.cs",
        "DiffLineRanges.cs", "CountText.cs",
    ];

    [GeneratedRegex(
        @"""[^""]*(No match|no match|No matching|no matching|Nothing (to|was|matches)|nothing (to|was|matches)"
        + @"|unchanged|No search|Loading\.\.\.)")]
    private static partial Regex WordingLiteral();

    [GeneratedRegex(@"new\(""(Esc|Enter|j/k|Ctrl-g|Ctrl-s|n/Esc|Ctrl-j/k)"", ""[^""]")]
    private static partial Regex HintLiteral();

    [GeneratedRegex(@"\[\.\.7\]|HashLength\s*=\s*7|Math\.Min\(7,|ShortHashLength|HashWidth\s*=\s*7")]
    private static partial Regex HashLength();

    [GeneratedRegex(@"[=!]= 1 \?")]
    private static partial Regex InlinePlural();

    // A footer or a key row declared by a renderer: the hints come from HintSet, the rows from ActionRow.
    [GeneratedRegex(@"HintItem\[\]|List<HintItem>|new HintItem\(""|new\(""[^""]+"", (HintVerb\.|""[A-Za-z])")]
    private static partial Regex RendererHint();

    // A button drawn inside a text row writes its brackets through ButtonLabel.Bracketed.
    [GeneratedRegex(@"""\[[+x ]\]""")]
    private static partial Regex HandBuiltBracket();

    // The wheel step is data of DefaultMouseMap.
    [GeneratedRegex(@"const int \w*Wheel\w*\s*=")]
    private static partial Regex WheelConstant();

    [Fact]
    public void GivenTheSources_WhenScanningForEmptyAndNoMatchTexts_ThenOnlyUiTextsSpellsThem()
    {
        var offenders = Lines(file => !file.EndsWith("UiTexts.cs", StringComparison.Ordinal))
            .Where(line => WordingLiteral().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use UiTexts instead of a literal: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheSources_WhenScanningForHintLabels_ThenSharedKeysTakeTheirWordFromHintVerb()
    {
        var offenders = Lines(_ => true)
            .Where(line => HintLiteral().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use HintVerb for the label: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheSources_WhenScanningForAHashLength_ThenOnlyShortHashHoldsIt()
    {
        var offenders = Lines(file => !file.EndsWith("ShortHash.cs", StringComparison.Ordinal))
            .Where(line => HashLength().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use ShortHash instead of a number: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheSources_WhenScanningForInlinePlurals_ThenCountTextIsUsedInstead()
    {
        var offenders = Lines(file => !SingularChecks.Any(name => file.EndsWith(name, StringComparison.Ordinal)))
            .Where(line => InlinePlural().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use CountText.Plural instead of '== 1 ?': {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheRenderers_WhenScanningForHintDeclarations_ThenNoneDeclaresItsOwnFooterOrKeyRow()
    {
        var tui = $"{Path.DirectorySeparatorChar}Gitunia.Tui{Path.DirectorySeparatorChar}";
        var offenders = Lines(file => file.Contains(tui, StringComparison.Ordinal))
            .Where(line => RendererHint().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Declare the hint in HintSet: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheSources_WhenScanningForBracketedButtons_ThenOnlyButtonLabelWritesTheBrackets()
    {
        var offenders = Lines(_ => true)
            .Where(line => HandBuiltBracket().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Use ButtonLabel.Bracketed: {string.Join("; ", offenders)}");
    }

    [Fact]
    public void GivenTheSources_WhenScanningForWheelSteps_ThenOnlyTheDefaultMouseMapHoldsThem()
    {
        var offenders = Lines(file => !file.EndsWith("DefaultMouseMap.cs", StringComparison.Ordinal))
            .Where(line => WheelConstant().IsMatch(line.Text))
            .Select(line => line.Where)
            .ToList();

        Assert.True(offenders.Count == 0, $"Put the wheel step in DefaultMouseMap: {string.Join("; ", offenders)}");
    }

    private static IEnumerable<SourceLine> Lines(Func<string, bool> fileFilter)
    {
        var root = typeof(UiWordingLintTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepoRoot")?.Value ?? string.Empty;
        var separator = Path.DirectorySeparatorChar;
        var generated = new[] { $"{separator}obj{separator}", $"{separator}bin{separator}" };
        foreach (var project in SourceProjects)
        {
            var directory = Path.Combine(root, "src", project);
            Assert.True(Directory.Exists(directory), $"Source folder not found: {directory}");
            var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !generated.Any(file.Contains) && fileFilter(file));
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (!lines[index].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        yield return new SourceLine($"{Path.GetFileName(file)}:{index + 1}", lines[index]);
                    }
                }
            }
        }
    }

    private sealed record SourceLine(string Where, string Text);
}
