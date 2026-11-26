using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Execution;
using Gitunia.Git.Ignore;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Ignore;

/// <summary>
/// Adds an untracked file or folder of the Unstaged list to <c>.gitignore</c> (ux-workflows.md, workflow 2,
/// "Ignore"), as pure transitions <c>(IgnoreCommand, IgnoreContext) -> Effects</c>. A one-key prompt lists the
/// patterns (this path, its extension, its folder) and shows the exact line each adds; the answer carries that line
/// as it was offered, so a cursor that moved meanwhile changes nothing. Only untracked paths qualify, because
/// <c>.gitignore</c> does not apply to tracked ones. Nothing is lost, so there is no Confirm; the line is appended
/// and the status is read again, which drops the file from the list.
/// </summary>
public static class IgnoreReducer
{
    /// <summary>Shown when an ignore is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: the last ignore is still running";

    private const string NotLoadedNotice = "The status is not loaded yet";
    private const string SelectNotice = "Select an untracked file or folder first";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(IgnoreCommand command, IgnoreContext context) => command switch
    {
        IgnoreCancelled => [],
        IgnoreChosen chosen => Chosen(chosen, context),
        IgnoreFailed failed => [Post(failed.Text, StatusSeverity.Error)],
        IgnoreRequested => Requested(context),
        IgnoreSucceeded succeeded => [Post(succeeded.Text, succeeded.Severity)],
        _ => throw new UnhandledCommandException(command),
    };

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Refuse(string text) => [Post(text)];

    private static ImmutableArray<AppEffect> Requested(IgnoreContext context)
    {
        if (context.FocusedPane == PaneKind.WorkingStaged)
        {
            return Refuse("Ignore works in the Unstaged list: unstage the file first");
        }

        if (context.FocusedPane is not (PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff))
        {
            return Refuse("Focus a file list first");
        }

        if (context.Tree is not { } tree)
        {
            return Refuse(NotLoadedNotice);
        }

        if (context.IsBusy)
        {
            return Refuse(BusyNotice);
        }

        if (tree.Selection is not { Section: WorkingTreeSection.Unstaged } selection)
        {
            return Refuse(SelectNotice);
        }

        if (selection.IsFolder)
        {
            return AskFolder(tree, selection.Path, context);
        }

        var entry = tree.SelectedEntry;

        return entry is null ? Refuse(SelectNotice) : AskFile(entry, context);
    }

    private static ImmutableArray<AppEffect> AskFile(StatusEntry entry, IgnoreContext context)
    {
        var name = PrintableText.Of(entry.Path);
        if (entry.Kind != StatusEntryKind.Untracked)
        {
            return Refuse($"{name} is tracked: .gitignore does not apply to it (git rm --cached stops tracking it)");
        }

        if (Unwritable(entry.Path) is { } refusal)
        {
            return Refuse(refusal);
        }

        var isFolder = entry.Path.EndsWith('/');

        return Ask(entry.Path, isFolder, context);
    }

    private static ImmutableArray<AppEffect> AskFolder(WorkingTreeState tree, string folder, IgnoreContext context)
    {
        var name = PrintableText.Of(folder);
        if (!tree.SelectedFolderEntries.Any(entry => entry.Kind == StatusEntryKind.Untracked))
        {
            return Refuse($"{name} has no untracked files: .gitignore does not apply to tracked files");
        }

        if (Unwritable(folder) is { } refusal)
        {
            return Refuse(refusal);
        }

        return Ask(folder, true, context);
    }

    private static string? Unwritable(string path)
    {
        if (GitPathText.HasEscapedBytes(path) || IgnorePatterns.IsUnwritable(path))
        {
            var name = PrintableText.Of(path);

            return $"{name} has a name that cannot be written to {IgnoreWriter.FileName}: edit the file by hand";
        }

        return null;
    }

    private static ImmutableArray<AppEffect> Ask(string path, bool isFolder, IgnoreContext context)
    {
        var request = new PromptRequest(
            $"Add to {IgnoreWriter.FileName}",
            PrintableText.Of(path),
            string.Empty,
            new IgnoreAnswer(context.TabId),
            Owner: context.TabId,
            KeyChoices: IgnorePatterns.Options(path, isFolder));

        return [new ShowPromptEffect(request)];
    }

    // The tab may have started another ignore since the prompt opened.
    private static ImmutableArray<AppEffect> Chosen(IgnoreChosen chosen, IgnoreContext context)
    {
        if (chosen.Pattern.Length == 0)
        {
            return [];
        }

        if (context.IsBusy)
        {
            return Refuse($"{BusyNotice}; {UiTexts.NothingWas("added")}");
        }

        return [new AddIgnorePatternEffect(context.TabId, context.Location, chosen.Pattern)];
    }
}
