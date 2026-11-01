using System.Collections.Immutable;
using Gitunia.Core.IssueTracker.Models;

namespace Gitunia.Tui.App.Details;

/// <summary>What the issue link keys read from the detail pane.</summary>
public interface IIssueLinkView
{
    /// <summary>The detail pane has focus and shows the Details tab.</summary>
    bool IsMessageFocused();

    /// <summary>The issue links of the selected row of the commit message, empty when there is none.</summary>
    ImmutableArray<IssueReference> LinksOfSelectedRow();
}
