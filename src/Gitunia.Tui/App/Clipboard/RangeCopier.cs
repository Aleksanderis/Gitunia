using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Copies the rows of a Visual-mode range of the graph, detail or diff pane through the clipboard.</summary>
/// <param name="clipboard">Copies the text.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
public sealed class RangeCopier(ClipboardCopier clipboard, Func<TabSession?> activeSession)
{
    private static readonly RangeCopyResult NothingToCopy = new(false, "Nothing to copy");

    /// <summary>Copies the range of <paramref name="pane"/>.</summary>
    /// <returns>Whether the text was copied, with a notice for the status bar when nothing was copied (a failed copy posts its own warning).</returns>
    public RangeCopyResult Copy(PaneKind pane, RowRange range)
    {
        var session = activeSession();
        if (session is null)
        {
            return NothingToCopy;
        }

        string text;
        string description;
        switch (pane)
        {
            case PaneKind.Graph:
                text = RangeTextBuilder.Graph(session.Graph, range);
                description = "commit hashes";
                break;
            case PaneKind.Detail:
                text = RangeTextBuilder.Detail(session.DetailSource, range);
                description = "detail rows";
                break;
            case PaneKind.Diff when session.Diff is { } diff:
                text = RangeTextBuilder.Diff(diff.Source, range);
                description = "diff rows";
                break;
            default:
                return NothingToCopy;
        }

        if (text.Length == 0)
        {
            return NothingToCopy;
        }

        var lines = text.Count(character => character == '\n');
        var copied = clipboard.Copy(text, lines == 1 ? $"1 line of {description}" : $"{lines} lines of {description}");
        return new RangeCopyResult(copied, string.Empty);
    }
}
