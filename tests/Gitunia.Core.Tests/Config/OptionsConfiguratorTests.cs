using System.Text.Json;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public sealed class OptionsConfiguratorTests
{
    private static readonly string Home = Path.Combine(Path.GetTempPath(), "gitunia-home");
    private static readonly string Existing = Path.Combine(Path.GetTempPath(), "gitunia-existing");

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static string Quote(string path) => $"\"{path.Replace("\\", "\\\\", StringComparison.Ordinal)}\"";

    private static bool Exists(string path) => path == Existing || path == Path.Combine(Home, "code");

    [Theory]
    [InlineData("{ \"showBranch\": true }", true)]
    [InlineData("{ \"showBranch\": false }", false)]
    [InlineData("{ \"showBranch\": null }", false)]
    [InlineData("{}", false)]
    [InlineData("{ \"other\": 1, \"showBranch\": true }", true)] // A foreign key is skipped
    [InlineData("null", false)]
    public void GivenAValidTabsObject_WhenParsing_ThenShowBranchFollowsItWithoutProblems(string json, bool expected)
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(expected, OptionsConfigurator.ParseShowBranch(Json(json), problems));
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("{ \"showBranch\": \"yes\" }", "tabs.showBranch")]
    [InlineData("{ \"showBranch\": 1 }", "tabs.showBranch")]
    [InlineData("5", "tabs")]
    [InlineData("[true]", "tabs")]
    public void GivenAnInvalidTabsValue_WhenParsing_ThenFalseWithAProblemOnTheKey(string json, string path)
    {
        var problems = new List<ConfigProblem>();

        Assert.False(OptionsConfigurator.ParseShowBranch(Json(json), problems));
        Assert.Equal(path, Assert.Single(problems).Path);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", false)]
    public void GivenAValidWatchFilesValue_WhenParsing_ThenItFollowsWithoutProblems(string json, bool expected)
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(expected, OptionsConfigurator.ParseWatchFiles(Json(json), problems));
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenNoWatchFilesValue_WhenParsing_ThenWatchingIsOff()
    {
        var problems = new List<ConfigProblem>();

        Assert.False(OptionsConfigurator.ParseWatchFiles(null, problems));
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenAWrongWatchFilesType_WhenParsing_ThenWatchingIsOffWithAProblem()
    {
        var problems = new List<ConfigProblem>();

        Assert.False(OptionsConfigurator.ParseWatchFiles(Json("\"yes\""), problems));
        Assert.Equal("watchFiles", Assert.Single(problems).Path);
    }

    [Theory]
    [InlineData("\"none\"", LabelCapStyle.None)]
    [InlineData("\"rounded\"", LabelCapStyle.Rounded)]
    [InlineData("\"Angled\"", LabelCapStyle.Angled)]
    [InlineData("null", LabelCapStyle.Rounded)]
    public void GivenAValidLabelCapsValue_WhenParsing_ThenItFollowsWithoutProblems(string json, LabelCapStyle expected)
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(expected, OptionsConfigurator.ParseLabelCaps(Json(json), problems));
        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("\"round\"")]
    [InlineData("true")]
    [InlineData("3")]
    public void GivenAnInvalidLabelCapsValue_WhenParsing_ThenRoundedWithAProblemOnTheKey(string json)
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(LabelCapStyle.Rounded, OptionsConfigurator.ParseLabelCaps(Json(json), problems));
        Assert.Equal("labelCaps", Assert.Single(problems).Path);
    }

    [Fact]
    public void GivenNoLabelCapsValue_WhenParsing_ThenRoundedCapsApply()
    {
        var problems = new List<ConfigProblem>();

        Assert.Equal(LabelCapStyle.Rounded, OptionsConfigurator.ParseLabelCaps(null, problems));
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenNoScanRoots_WhenParsing_ThenNullMeansTheConventionalFolders()
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(OptionsConfigurator.ParseScanRoots(null, Home, Exists, problems));
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenAbsoluteAndHomeRelativeRoots_WhenParsing_ThenBothAreKeptInOrderWithTheHomeExpanded()
    {
        var problems = new List<ConfigProblem>();
        var text = $"[{Quote(Existing)}, \"~/code\"]";

        var roots = OptionsConfigurator.ParseScanRoots(Json(text), Home, Exists, problems);

        Assert.Equal([Existing, Path.Combine(Home, "code")], roots);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenAnEmptyList_WhenParsing_ThenNoRootsAreScanned()
    {
        var problems = new List<ConfigProblem>();

        var roots = OptionsConfigurator.ParseScanRoots(Json("[]"), Home, Exists, problems);

        Assert.NotNull(roots);
        Assert.Empty(roots.Value);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenSomeUnusableEntries_WhenParsing_ThenTheUsableOnesStayAndEachBadOneIsReportedByIndex()
    {
        var problems = new List<ConfigProblem>();
        var text = $"[{Quote(Existing)}, 5, \"\", \"relative/dir\", \"~/absent\"]";

        var roots = OptionsConfigurator.ParseScanRoots(Json(text), Home, Exists, problems);

        Assert.Equal([Existing], roots);
        var paths = problems.Select(problem => problem.Path);
        Assert.Equal(["scanRoots[1]", "scanRoots[2]", "scanRoots[3]", "scanRoots[4]"], paths);
    }

    [Fact]
    public void GivenOnlyUnusableEntries_WhenParsing_ThenTheConventionalFoldersApply()
    {
        var problems = new List<ConfigProblem>();

        var roots = OptionsConfigurator.ParseScanRoots(Json("[\"relative\", 3]"), Home, Exists, problems);

        Assert.Null(roots);
        Assert.Equal(2, problems.Count);
    }

    [Fact]
    public void GivenTheSameRootWithATrailingSeparator_WhenParsing_ThenItIsListedOnce()
    {
        var problems = new List<ConfigProblem>();
        var text = $"[{Quote(Existing)}, {Quote(Existing + Path.DirectorySeparatorChar)}]";

        var roots = OptionsConfigurator.ParseScanRoots(Json(text), Home, Exists, problems);

        Assert.Equal([Existing], roots);
    }

    [Fact]
    public void GivenARootThatIsRootedButNotFullyQualified_WhenParsing_ThenItIsSkippedAndReported()
    {
        var problems = new List<ConfigProblem>();
        var rootedOnly = OperatingSystem.IsWindows() ? "\\srv\\git" : "relative";

        var roots = OptionsConfigurator.ParseScanRoots(Json($"[{Quote(rootedOnly)}]"), Home, _ => true, problems);

        Assert.Null(roots);
        Assert.Equal("scanRoots[0]", Assert.Single(problems).Path);
    }

    [Fact]
    public void GivenTheSameRootTwice_WhenParsing_ThenItIsListedOnce()
    {
        var problems = new List<ConfigProblem>();
        var text = $"[{Quote(Existing)}, {Quote(Existing)}]";

        var roots = OptionsConfigurator.ParseScanRoots(Json(text), Home, Exists, problems);

        Assert.Equal([Existing], roots);
    }

    [Theory]
    [InlineData("\"~/code\"")]
    [InlineData("{ \"path\": 1 }")]
    public void GivenScanRootsOfTheWrongType_WhenParsing_ThenTheDefaultAppliesWithAProblem(string json)
    {
        var problems = new List<ConfigProblem>();

        Assert.Null(OptionsConfigurator.ParseScanRoots(Json(json), Home, Exists, problems));
        Assert.Equal("scanRoots", Assert.Single(problems).Path);
    }

    [Fact]
    public void GivenAFileWithAllOptions_WhenReading_ThenEveryOptionIsSet()
    {
        var problems = new List<ConfigProblem>();
        var json = $$"""
            { "frameStyle": "square", "tabs": { "showBranch": true }, "scanRoots": [{{Quote(Existing)}}] }
            """;
        var document = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.ConfigDocument)!;

        var options = OptionsConfigurator.Read(document, Home, Exists, problems);

        Assert.True(options.ShowBranchInTab);
        Assert.Equal([Existing], options.ScanRoots);
        Assert.Empty(problems);
    }

    [Fact]
    public void GivenAnEmptyDocument_WhenReading_ThenTheDefaultsApply()
    {
        var problems = new List<ConfigProblem>();

        var options = OptionsConfigurator.Read(new ConfigDocument(), Home, Exists, problems);

        Assert.Equal(ConfigOptions.Default, options);
        Assert.Empty(problems);
    }
}
