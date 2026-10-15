namespace Gitunia.Platform.ExternalTools;

/// <summary>A program that gets the real terminal: its name, arguments and the directory it runs in.</summary>
/// <param name="FileName">Program name or path.</param>
/// <param name="Arguments">Arguments, one per item (no shell is involved).</param>
/// <param name="WorkingDirectory">Directory the program starts in (a repository or worktree path, never the process cwd).</param>
public sealed record ExternalToolRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory);
