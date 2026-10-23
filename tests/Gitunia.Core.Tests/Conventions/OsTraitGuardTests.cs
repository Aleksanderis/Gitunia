using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gitunia.Core.Tests.Conventions;

/// <summary>
/// Keeps the Windows CI filter (<c>Os!=Independent</c>, #637) honest: a file marked <c>Os=Independent</c> must not
/// touch the file system, processes, environment or real git, and the git and platform test projects are never marked.
/// </summary>
public sealed partial class OsTraitGuardTests
{
    private const string Marker = "[Trait(\"Os\", \"Independent\")]";

    [GeneratedRegex(
        @"TestRepository|IsolatedGit|TestGit|GitRunner|\bPath\.|\bFile\.|\bDirectory\.|Environment\.|Process"
        + @"|Console\.|Symlink|OperatingSystem|Encoding\.|Temp")]
    private static partial Regex OsSensitiveApi();

    [Fact]
    public void GivenFilesMarkedOsIndependent_WhenScanningTheirSource_ThenNoneUsesAnOsSensitiveApi()
    {
        var offenders = MarkedFiles("Gitunia.Core.Tests")
            .Concat(MarkedFiles("Gitunia.Tui.Tests"))
            .Select(file => (file, match: OsSensitiveApi().Match(File.ReadAllText(file))))
            .Where(entry => entry.match.Success)
            .Select(entry => $"{entry.file}: {entry.match.Value}")
            .ToList();

        Assert.True(offenders.Count == 0, "Marked Os=Independent but OS-sensitive: " + string.Join("; ", offenders));
    }

    [Theory]
    [InlineData("Gitunia.Git.Tests")]
    [InlineData("Gitunia.Platform.Tests")]
    public void GivenTheGitAndPlatformTestProjects_WhenScanningForTheMarker_ThenNothingIsMarked(string project)
    {
        Assert.Empty(MarkedFiles(project));
    }

    private static IEnumerable<string> MarkedFiles(string project)
    {
        var root = typeof(OsTraitGuardTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "RepoRoot")?.Value ?? string.Empty;
        var directory = Path.Combine(root, "tests", project);
        Assert.True(Directory.Exists(directory), $"Test project folder not found: {directory}");
        var generated = new[]
        {
            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
            Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
        };

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !generated.Any(file.Contains))
            .Where(file => File.ReadAllText(file).Contains(Marker, StringComparison.Ordinal));
    }
}
