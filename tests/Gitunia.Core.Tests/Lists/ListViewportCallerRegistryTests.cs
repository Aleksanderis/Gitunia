using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Core.Tests.Lists;

/// <summary>
/// Every file in <c>src</c> that calls <c>ListViewport</c> is listed in <see cref="Callers"/> with
/// the test file that holds its scenarios (k at the top, G, Ctrl-d at the end, a count, a smaller window, an empty list)
/// (always <c>ListViewportContractTests</c> or <c>ListWindowContractTests</c>). A new caller
/// fails <see cref="GivenASourceFileCallingListViewport_WhenScanningSrc_ThenItIsRegistered"/> until it is added here and
/// covered. The Terminal.Gui <c>ListView</c> surfaces (graph, diff, sidebar, detail) use the framework's viewport and are
/// not part of this contract.
/// </summary>
public sealed partial class ListViewportCallerRegistryTests
{
    private const string CoreContract = "tests/Gitunia.Core.Tests/Lists/ListViewportContractTests.cs";
    private const string WindowContract = "tests/Gitunia.Tui.Tests/Rendering/Overlays/ListWindowContractTests.cs";

    /// <summary>The caller (path under the repository root) and the test file with its scenarios.</summary>
    private static readonly IReadOnlyDictionary<string, string> Callers = new Dictionary<string, string>
    {
        // State cursors: the move stops at both ends (Core contract surfaces).
        ["src/Gitunia.Core/FileHistory/FileHistoryState.cs"] = CoreContract,
        ["src/Gitunia.Core/Filtering/FilterList.cs"] = CoreContract,
        ["src/Gitunia.Core/Rebase/RebaseEditorState.cs"] = CoreContract,
        ["src/Gitunia.Core/Reflog/ReflogState.cs"] = CoreContract,
        ["src/Gitunia.Core/Settings/SettingsState.cs"] = CoreContract,
        ["src/Gitunia.Core/Conflicts/ConflictEditorState.cs"] = CoreContract,

        // Editor scrolling (EdgeTopFor): the Core contract holds the arithmetic, the window contract the drawn frames.
        ["src/Gitunia.Core/Conflicts/ConflictEditorState.Scrolling.cs"] = CoreContract,
        ["src/Gitunia.Tui/Rendering/Panes/MessageEditorRenderer.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Screen/ScreenComposer.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/WorkingTree/MessageBoxRenderer.cs"] = WindowContract,

        // Windowing renderers: the selected row stays inside the shown rows.
        ["src/Gitunia.Tui/Rendering/Overlays/FileHistoryFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/GroupedRows.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/RebaseEditorFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/ReflogFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/BlameFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/HelpFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/PromptPopupFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/SettingsFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Screen/PickerLayout.cs"] = WindowContract,
    };

    [Fact]
    public void GivenASourceFileCallingListViewport_WhenScanningSrc_ThenItIsRegistered()
    {
        var root = RepositoryRoot();
        var found = CallersInSource(root);

        var missing = found.Except(Callers.Keys).Order().ToList();
        var stale = Callers.Keys.Except(found).Order().ToList();

        Assert.True(missing.Count == 0, $"Register in {nameof(ListViewportCallerRegistryTests)}: {string.Join(", ", missing)}");
        Assert.True(stale.Count == 0, $"No longer calls ListViewport, remove from the registry: {string.Join(", ", stale)}");
    }

    [Fact]
    public void GivenARegisteredCaller_WhenLookingForItsScenarios_ThenItIsAContractTestFile()
    {
        Assert.All(Callers.Values, tests => Assert.True(tests is CoreContract or WindowContract, tests));
    }

    [Fact]
    public void GivenARegisteredCaller_WhenLookingForItsScenarios_ThenTheTestFileExists()
    {
        var root = RepositoryRoot();

        foreach (var (caller, tests) in Callers)
        {
            Assert.True(File.Exists(Path.Combine(root, tests)), $"{caller}: missing test file {tests}");
        }
    }

    private static List<string> CallersInSource(string root)
    {
        var source = Path.Combine(root, "src");
        var viewport = Path.Combine(source, "Gitunia.Core", "Lists", "ListViewport.cs");
        var callers = new List<string>();

        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (file == viewport || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            if (File.ReadLines(file).Any(IsCodeReference))
            {
                callers.Add(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        return callers;
    }

    [GeneratedRegex(@"\bListViewport\b")]
    private static partial Regex ListViewportWord();

    private static bool IsCodeReference(string line) =>
        ListViewportWord().IsMatch(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private static string RepositoryRoot()
    {
        var root = typeof(ListViewportCallerRegistryTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepoRoot")?.Value ?? string.Empty;
        Assert.True(Directory.Exists(Path.Combine(root, "src")), $"Repository root not found: {root}");

        return root;
    }
}
