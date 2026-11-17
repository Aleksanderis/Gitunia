using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Core.Tests.Lists;

/// <summary>
/// Every file in <c>src</c> that calls <c>ListViewport</c> is listed in <see cref="Callers"/> with
/// the test file that holds its scenarios (k at the top, G, Ctrl-d at the end, a count, a smaller window, an empty list)
/// when that is <c>ListViewportContractTests</c> or <c>ListWindowContractTests</c>, otherwise the nearest existing test
/// (the surface has no contract scenarios yet, see the follow-up issue). A new caller
/// fails <see cref="GivenASourceFileCallingListViewport_WhenScanningSrc_ThenItIsRegistered"/> until it is added here and
/// covered. The Terminal.Gui <c>ListView</c> surfaces (graph, diff, sidebar, detail) use the framework's viewport and are
/// not part of this contract.
/// </summary>
public sealed partial class ListViewportCallerRegistryTests
{
    private const string CoreContract = "tests/Gitunia.Core.Tests/Lists/ListViewportContractTests.cs";
    private const string WindowContract = "tests/Gitunia.Tui.Tests/Rendering/Overlays/ListWindowContractTests.cs";
    private const string SettingsReducer = "tests/Gitunia.Core.Tests/Settings/SettingsReducerTests.cs";
    private const string PromptPopupFrame = "tests/Gitunia.Tui.Tests/Rendering/Overlays/PromptPopupFrameBuilderTests.cs";
    private const string SettingsFrame = "tests/Gitunia.Tui.Tests/Rendering/Overlays/SettingsFrameBuilderTests.cs";
    private const string HelpFrame = "tests/Gitunia.Tui.Tests/Rendering/Overlays/HelpAllCommandsFrameTests.cs";
    private const string BlameInput = "tests/Gitunia.Tui.Tests/App/Blame/BlameInputTests.cs";
    private const string PickerComposer = "tests/Gitunia.Tui.Tests/Rendering/Screen/PickerComposerTests.cs";
    private const string ScreenComposer = "tests/Gitunia.Tui.Tests/Rendering/Screen/ScreenComposerTests.cs";
    private const string MessageBox = "tests/Gitunia.Tui.Tests/Rendering/WorkingTree/MessageBoxRendererTests.cs";
    private const string ConflictFrame = "tests/Gitunia.Tui.Tests/Rendering/Overlays/ConflictEditorFrameBuilderTests.cs";

    /// <summary>The caller (path under the repository root) and the test file with its scenarios.</summary>
    private static readonly IReadOnlyDictionary<string, string> Callers = new Dictionary<string, string>
    {
        // State cursors: the move stops at both ends (Core contract surfaces).
        ["src/Gitunia.Core/FileHistory/FileHistoryState.cs"] = CoreContract,
        ["src/Gitunia.Core/Overlays/PaletteState.cs"] = CoreContract,
        ["src/Gitunia.Core/Prompts/PromptPicker.cs"] = CoreContract,
        ["src/Gitunia.Core/Rebase/RebaseEditorState.cs"] = CoreContract,
        ["src/Gitunia.Core/Reflog/ReflogRefPicker.cs"] = CoreContract,
        ["src/Gitunia.Core/Reflog/ReflogState.cs"] = CoreContract,
        ["src/Gitunia.Core/Search/Models/RefJumpState.cs"] = CoreContract,
        ["src/Gitunia.Core/Settings/SettingsState.cs"] = SettingsReducer,
        ["src/Gitunia.Core/Conflicts/ConflictEditorState.cs"] = ConflictFrame,

        // Editor scrolling (EdgeTopFor): the Core contract holds its scenarios.
        ["src/Gitunia.Core/Conflicts/ConflictEditorState.Scrolling.cs"] = CoreContract,
        ["src/Gitunia.Tui/Rendering/Panes/MessageEditorRenderer.cs"] = CoreContract,
        ["src/Gitunia.Tui/Rendering/Screen/ScreenComposer.cs"] = ScreenComposer,
        ["src/Gitunia.Tui/Rendering/WorkingTree/MessageBoxRenderer.cs"] = MessageBox,

        // Windowing renderers: the selected row stays inside the shown rows.
        ["src/Gitunia.Tui/Rendering/Overlays/FileHistoryFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/GroupedRows.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/RebaseEditorFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/ReflogFrameBuilder.cs"] = WindowContract,
        ["src/Gitunia.Tui/Rendering/Overlays/BlameFrameBuilder.cs"] = BlameInput,
        ["src/Gitunia.Tui/Rendering/Overlays/HelpFrameBuilder.cs"] = HelpFrame,
        ["src/Gitunia.Tui/Rendering/Overlays/PromptPopupFrameBuilder.cs"] = PromptPopupFrame,
        ["src/Gitunia.Tui/Rendering/Overlays/SettingsFrameBuilder.cs"] = SettingsFrame,
        ["src/Gitunia.Tui/Rendering/Screen/PickerLayout.cs"] = PickerComposer,
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
