using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Discard;

/// <summary>
/// Discards unstaged changes (ux-workflows.md, workflow 2, "Discard"), as pure transitions
/// <c>(DiscardCommand, DiscardContext) -> Effects</c>: a file from the Unstaged list, the files of a folder or a Visual
/// range, or the hunk or Visual lines of its diff. Nothing is undoable, so nothing runs before the user answers <c>y</c>
/// in a dialog that names what is lost; the answer carries that target, taken from the lists or the diff when the dialog
/// opened, so a cursor that moved meanwhile changes nothing. One discard runs per tab and it excludes staging and the
/// other operations that write the index or the working tree.
/// </summary>
public static class DiscardReducer
{
    /// <summary>Shown when a discard is asked for while a stage, unstage or discard runs.</summary>
    public const string BusyNotice = "Busy: the last stage, unstage or discard is still running";

    /// <summary>Shown when another operation of the tab is changing the index or the working tree.</summary>
    public const string BlockedNotice = "Busy: another operation of this tab is still running";

    private const string TooSmallNotice = "The screen is too small to ask: nothing was discarded";
    private const string NotLoadedNotice = "The status is not loaded yet";
    private const string SelectNotice = "Select a file first";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(DiscardCommand command, DiscardContext context) => command switch
    {
        DiscardCancelled => [],
        DiscardConfirmed confirmed => Confirmed(confirmed, context),
        DiscardFailed failed => [Post(failed.Text, StatusSeverity.Error)],
        DiscardRequested requested => Requested(requested, context),
        DiscardSelectionRequested selection => SelectionRequested(selection, context),
        DiscardSucceeded succeeded => Succeeded(succeeded),
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Refuse(string text) => [Post(text)];

    private static ImmutableArray<AppEffect> Ask(
        ConfirmPrompt prompt,
        DiscardContext context,
        DiscardTarget target,
        bool endsRange) =>
        [new ShowConfirmEffect(prompt, new DiscardConfirmed(context.TabId, target, endsRange), TooSmallNotice)];

    private static string? Busy(DiscardContext context)
    {
        if (context.IsBusy)
        {
            return BusyNotice;
        }

        return context.IsBlocked ? BlockedNotice : null;
    }

    private static ImmutableArray<AppEffect> Requested(DiscardRequested requested, DiscardContext context)
    {
        var isRange = requested.Range is not null;
        var focus = context.FocusedPane;
        if (focus == PaneKind.WorkingStaged)
        {
            return Refuse(isRange
                ? "Discard works in the Unstaged list: unstage the files first"
                : "Discard works in the Unstaged list: unstage the file first");
        }

        if (focus != PaneKind.WorkingUnstaged)
        {
            return Refuse("Focus a file list first");
        }

        if (context.Tree is not { } tree)
        {
            return Refuse(NotLoadedNotice);
        }

        if (Busy(context) is { } busy)
        {
            return Refuse(busy);
        }

        if (requested.Range is { } range)
        {
            return AskRange(tree, range, context);
        }

        if (tree.Selection is { IsFolder: true, Section: WorkingTreeSection.Unstaged } folder)
        {
            return AskFolder(tree, folder.Path, context);
        }

        var entry = default(StatusEntry);
        if (tree.Selection is { Section: WorkingTreeSection.Unstaged } selection)
        {
            entry = tree.Unstaged.FirstOrDefault(candidate => candidate.Path == selection.Path);
        }

        if (entry is null)
        {
            return Refuse(SelectNotice);
        }

        var refusal = Refusal(entry, context);
        if (refusal.Length > 0)
        {
            return Refuse(refusal);
        }

        return Ask(DiscardPrompts.ForFile(entry), context, new DiscardFile(entry), false);
    }

    private static ImmutableArray<AppEffect> AskRange(WorkingTreeState tree, RowRange range, DiscardContext context)
    {
        var rows = tree.RowsOf(WorkingTreeSection.Unstaged);
        if (rows.Count == 0)
        {
            return Refuse(SelectNotice);
        }

        var first = Math.Clamp(range.First, 0, rows.Count - 1);
        var last = Math.Clamp(range.Last, first, rows.Count - 1);
        var picked = rows.EntriesOfRows(first, last).Select(index => tree.Unstaged[index]).ToList();

        return AskEntries(picked, null, true, context);
    }

    // The folder's dialog names the folder and counts every file under it, hidden ones in a closed folder included.
    private static ImmutableArray<AppEffect> AskFolder(WorkingTreeState tree, string folder, DiscardContext context)
    {
        var picked = tree.SelectedFolderEntries.ToList();
        if (picked.Count == 0)
        {
            return Refuse(SelectNotice);
        }

        return AskEntries(picked, $"{PrintableText.Of(folder)}{DiscardPrompts.DirectorySuffix}", false, context);
    }

    private static ImmutableArray<AppEffect> AskEntries(
        List<StatusEntry> picked,
        string? folderName,
        bool endsRange,
        DiscardContext context)
    {
        var usable = picked.Where(entry => Refusal(entry, context).Length == 0).ToList();
        var skipped = picked.Count - usable.Count;
        if (usable.Count == 0)
        {
            return Refuse(Refusal(picked[0], context));
        }

        if (usable.Count == 1 && skipped == 0 && folderName is null)
        {
            return Ask(DiscardPrompts.ForFile(usable[0]), context, new DiscardFile(usable[0]), endsRange);
        }

        var prompt = DiscardPrompts.ForFiles(usable, skipped, folderName);

        return Ask(prompt, context, new DiscardFiles(usable, skipped), endsRange);
    }

    private static ImmutableArray<AppEffect> SelectionRequested(
        DiscardSelectionRequested request,
        DiscardContext context)
    {
        var selection = request.Selection;
        var entry = selection.Entry;
        if (selection.Side != WorkingDiffSide.Unstaged)
        {
            return Refuse("Discard works in the Unstaged diff: unstage the changes first");
        }

        if (Busy(context) is { } busy)
        {
            return Refuse(busy);
        }

        var refusal = Refusal(entry, context);
        if (refusal.Length > 0)
        {
            return Refuse(refusal);
        }

        if (selection.IsRange && DiscardPrompts.IsWholeFileOnly(entry))
        {
            var name = PrintableText.Of(entry.Path);

            return Refuse($"{name} cannot be split into lines: discard the whole file from the list");
        }

        return Ask(
            DiscardPrompts.ForSelection(selection),
            context,
            new DiscardLines(selection, request.Move),
            selection.IsRange);
    }

    // The cases that never reach the dialog: nothing to discard here, or no safe way to do it.
    private static string Refusal(StatusEntry entry, DiscardContext context)
    {
        var name = PrintableText.Of(entry.Path);
        if (entry.Kind == StatusEntryKind.Unmerged)
        {
            return $"{name} is conflicted: resolve it first (a conflict is not discarded from here)";
        }

        if (entry.Submodule is not null)
        {
            return $"{name} is a submodule: discard its changes inside it";
        }

        if (entry.Kind == StatusEntryKind.Untracked
            && GitPathText.HasEscapedBytes(entry.Path)
            && !context.CanDeleteInvalidNames)
        {
            return $"{name} has a name that is not valid UTF-8, which this OS cannot delete from here: delete it by hand";
        }

        return string.Empty;
    }

    // Nothing was decided at the dialog but what to throw away; the tab may have started something else since.
    private static ImmutableArray<AppEffect> Confirmed(DiscardConfirmed confirmed, DiscardContext context)
    {
        if (Busy(context) is { } busy)
        {
            return Refuse($"{busy}; nothing was discarded");
        }

        var tabId = context.TabId;
        var location = context.Location;

        return confirmed.Target switch
        {
            DiscardFile file => [FileEffect(tabId, location, file.Entry)],
            DiscardFiles files => [new DiscardFilesEffect(tabId, location, files.Files, files.Skipped)],
            DiscardLines lines => [LinesEffect(tabId, location, lines)],
            _ => throw new UnhandledCommandException(confirmed),
        };
    }

    private static DiscardSelectionEffect FileEffect(TabId tabId, RepositoryLocation location, StatusEntry entry)
    {
        var selection = new StagingSelection(WorkingDiffSide.Unstaged, entry, [], false);

        return new DiscardSelectionEffect(tabId, location, selection, true, null);
    }

    // The whole file goes only when the dialog said so; a hunk that cannot be cut is refused, never widened.
    private static DiscardSelectionEffect LinesEffect(TabId tabId, RepositoryLocation location, DiscardLines lines)
    {
        var selection = lines.Selection;
        var allowWholeFile = !selection.IsRange && DiscardPrompts.NamesWholeFile(selection);

        return new DiscardSelectionEffect(tabId, location, selection, allowWholeFile, lines.Move);
    }

    private static ImmutableArray<AppEffect> Succeeded(DiscardSucceeded succeeded)
    {
        var post = Post(succeeded.Text, succeeded.Severity);
        if (succeeded.Move is { } move)
        {
            return [post, new MoveDiffCursorEffect(succeeded.TabId, move)];
        }

        return [post];
    }
}
