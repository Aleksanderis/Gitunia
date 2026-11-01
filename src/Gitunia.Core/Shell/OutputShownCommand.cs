namespace Gitunia.Core.Shell;

/// <summary>Show the output of a failed command in a scrollable box. An open dialog is replaced (the output is the
/// newer fact).</summary>
/// <param name="Title">The box title.</param>
/// <param name="Heading">The heading of the text section.</param>
/// <param name="Lines">The output, one entry per line.</param>
public sealed record OutputShownCommand(string Title, string Heading, IReadOnlyList<string> Lines) : ShellCommand;
