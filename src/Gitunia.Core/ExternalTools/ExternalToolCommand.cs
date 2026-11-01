using Gitunia.Git.ExternalTools;

namespace Gitunia.Core.ExternalTools;

/// <summary>A command of the external tools (difftool, mergetool, commit with the editor).</summary>
public abstract record ExternalToolCommand : AppCommand
{
    private protected ExternalToolCommand()
    {
    }
}

/// <summary>
/// A tool key was pressed. The tool works on the active tab; <paramref name="Commit"/> is the commit selected in the
/// view (null when none), which only the difftool needs.
/// </summary>
public sealed record ExternalToolRequested(ExternalToolKind Kind, string? Commit) : ExternalToolCommand;
