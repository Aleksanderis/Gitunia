namespace Gitunia.Git.Diff.Models;

/// <summary>How a diff is read: the context around changes, whole files and whitespace handling.</summary>
/// <param name="ContextLines">Unchanged lines kept around each change (git's <c>-U</c>).</param>
/// <param name="WholeFile">Show every file whole, whatever <paramref name="ContextLines"/> says.</param>
/// <param name="IgnoreWhitespace">Leave out changes that only differ in whitespace (<c>-w</c>).</param>
public sealed record DiffOptions(
    int ContextLines = DiffOptions.DefaultContextLines,
    bool WholeFile = false,
    bool IgnoreWhitespace = false)
{
    private const string ContextArgumentPrefix = "-U";
    private const string IgnoreWhitespaceArgument = "-w";

    /// <summary>Git's own default.</summary>
    public const int DefaultContextLines = 3;

    /// <summary>Lines one step of <see cref="MoreContext"/> or <see cref="LessContext"/> changes.</summary>
    public const int ContextStep = 3;

    /// <summary>The most context lines asked for; beyond it use <see cref="WholeFile"/>.</summary>
    public const int MaximumContextLines = 999;

    /// <summary>Context given to git when <see cref="WholeFile"/> is on: more than any file has.</summary>
    public const int WholeFileContextLines = 1_000_000;

    public static DiffOptions Default { get; } = new();

    /// <summary>The options differ from what git shows by default (the pane marks that).</summary>
    public bool IsCustom => this != Default;

    public DiffOptions MoreContext() =>
        this with { ContextLines = Math.Min(MaximumContextLines, ContextLines + ContextStep), WholeFile = false };

    public DiffOptions LessContext() =>
        this with { ContextLines = Math.Max(0, ContextLines - ContextStep), WholeFile = false };

    /// <summary>The git arguments these options stand for.</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string>(2);
        var context = WholeFile ? WholeFileContextLines : ContextLines;
        if (context != DefaultContextLines)
        {
            arguments.Add($"{ContextArgumentPrefix}{context}");
        }

        if (IgnoreWhitespace)
        {
            arguments.Add(IgnoreWhitespaceArgument);
        }

        return arguments;
    }
}
