using System.Collections.Immutable;
using Gitunia.Core.IssueTracker.Models;

namespace Gitunia.Core.IssueLinks;

/// <summary>A command of the issue links in the commit message.</summary>
public abstract record IssueLinkCommand : AppCommand
{
    private protected IssueLinkCommand()
    {
    }
}

/// <summary>
/// A link of the selected message line was asked for. <paramref name="References"/> are the links of that line,
/// <paramref name="Number"/> the typed count (null: the first), <paramref name="IsExplicit"/> is true for the
/// open-link key (which says so when the line has none) and false for Enter, which then does something else.
/// </summary>
public sealed record IssueLinkOpenRequested(
    ImmutableArray<IssueReference> References,
    int? Number,
    bool IsExplicit) : IssueLinkCommand;

/// <summary>The system opener finished (or failed to start) for an address.</summary>
public sealed record IssueLinkOpenFinished(string Url, bool Opened, string? Error) : IssueLinkCommand;
