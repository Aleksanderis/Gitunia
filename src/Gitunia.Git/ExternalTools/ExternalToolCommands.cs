namespace Gitunia.Git.ExternalTools;

/// <summary>
/// Builds the git argument lists of the external tool hand-offs. The user's own configuration decides which editor
/// and which tool run (<c>diff.tool</c>, <c>merge.tool</c>, <c>core.editor</c>), so none is set here.
/// </summary>
public static class ExternalToolCommands
{
    private const string RepositoryDirectoryOption = "-C";
    private const string NoPromptOption = "--no-prompt";
    private const string DifftoolCommand = "difftool";
    private const string MergetoolCommand = "mergetool";
    private const string CommitCommand = "commit";
    private const string FirstParentSuffix = "^1";

    /// <summary>
    /// Arguments for <paramref name="kind"/>; <paramref name="commit"/> is required for the difftool.
    /// </summary>
    public static IReadOnlyList<string> Build(ExternalToolKind kind, string repositoryPath, string? commit = null) =>
        kind switch
        {
            ExternalToolKind.Difftool => BuildDifftool(repositoryPath, commit),
            ExternalToolKind.Mergetool => [RepositoryDirectoryOption, repositoryPath, MergetoolCommand],
            ExternalToolKind.CommitWithEditor => [RepositoryDirectoryOption, repositoryPath, CommitCommand],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    /// <summary>Prefixes <paramref name="arguments"/> with <c>-C &lt;repositoryPath&gt;</c>.</summary>
    public static IReadOnlyList<string> InRepository(string repositoryPath, IReadOnlyList<string> arguments) =>
        [RepositoryDirectoryOption, repositoryPath, .. arguments];

    /// <summary>The name shown in messages.</summary>
    public static string NameOf(ExternalToolKind kind) =>
        kind switch
        {
            ExternalToolKind.Difftool => DifftoolCommand,
            ExternalToolKind.Mergetool => MergetoolCommand,
            _ => "editor",
        };

    private static string[] BuildDifftool(string repositoryPath, string? commit)
    {
        if (commit is null)
        {
            throw new ArgumentException("The difftool needs a commit.", nameof(commit));
        }

        return
        [
            RepositoryDirectoryOption,
            repositoryPath,
            DifftoolCommand,
            NoPromptOption,
            $"{commit}{FirstParentSuffix}",
            commit,
        ];
    }
}
