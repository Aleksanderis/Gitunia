using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Reflog;

/// <summary>
/// The reflog overlay as pure transitions <c>(session, command) -> (session, effects)</c>. While the list is open it
/// consumes every action, so nothing behind it moves. Reads are an effect; the entry flows (reset, branch, checkout,
/// select in the graph) are an effect the terminal layer carries out.
/// </summary>
public static class ReflogReducer
{
    /// <summary>Entries read per open; older ones are not listed.</summary>
    public const int EntryLimit = 1000;

    /// <summary>The name of the ref whose reflog opens by default.</summary>
    public const string HeadName = "HEAD";

    /// <summary>The warning when an entry action runs on an empty list.</summary>
    public const string NoEntryNotice = "No reflog entry selected";

    /// <summary>The warning when a second read is asked for while one runs.</summary>
    public const string StillReadingNotice = "The reflog is still being read: choose again in a moment";

    /// <summary>The warning when a search step finds nothing and no search was typed.</summary>
    public const string NoSearchNotice = "No search: press / first";

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tabs the command may need.</param>
    public static ReflogUpdate Reduce(ReflogSession session, ReflogCommand command, ReflogContext context) =>
        command switch
        {
            ReflogActionPressed pressed => Press(session, pressed, context),
            ReflogEntryActionRefused refused => Warn(session, refused.Text),
            ReflogEntryActionStarted => Close(session),
            ReflogEntryClicked clicked => ClickEntry(session, clicked.Index),
            ReflogKeyTyped typed => TypeKey(session, typed.Key, context),
            ReflogLoaded loaded => Loaded(session, loaded),
            ReflogOpenRequested open => Open(session, open, context),
            ReflogPickerRowClicked clicked => ClickPickerRow(session, clicked.Index),
            ReflogReadCancelled => Done(session with { IsReading = false }),
            ReflogReadFailed failed => Failed(session, failed.Text),
            ReflogTabClosed => Close(session),
            _ => throw new UnhandledCommandException(command),
        };

    /// <summary>The tab-closed cleanup of the session: closes the list when its tab is gone, else null.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="workspace">The workspace after the tab command.</param>
    public static ReflogUpdate? OnTabClosed(ReflogSession session, Workspace workspace) =>
        session.List is { } list && workspace.FindTab(list.TabId) is null ? Close(session) : null;

    private static ReflogUpdate Open(ReflogSession session, ReflogOpenRequested open, ReflogContext context)
    {
        if (context.ActiveTab is not { } tab || session.IsReading)
        {
            return Done(session);
        }

        var isBranch = open.Branch is not null;
        var reference = isBranch ? $"{GitRefPrefixes.Heads}{open.Branch}" : HeadName;

        return Read(session, tab, reference, open.Branch ?? HeadName, refresh: false);
    }

    private static ReflogUpdate Press(ReflogSession session, ReflogActionPressed pressed, ReflogContext context)
    {
        if (session.List is not { } list)
        {
            return Done(session);
        }

        if (context.ListTab is null)
        {
            return Close(session);
        }

        var action = pressed.Action;
        if (list.Picker is { } picker)
        {
            return InPicker(session, list, picker, action, context);
        }

        if (list.IsEditingSearch)
        {
            return InSearchBox(session, list, action);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return list.HasSearch ? With(session, list.ClearSearch()) : Close(session);
        }

        if (action == ActionIds.AppQuit)
        {
            return Close(session);
        }

        if (action == ActionIds.SearchOpen)
        {
            return With(session, list.OpenSearch(), new SetKeyModeEffect(KeyMode.Insert));
        }

        if (action == ActionIds.SearchNext || action == ActionIds.SearchPrevious)
        {
            return StepMatch(session, list, action == ActionIds.SearchNext, pressed.Count);
        }

        if (action == ActionIds.ReflogOpen)
        {
            return OpenPicker(session, list, context.ListTab);
        }

        if (action == ActionIds.RepositoryRefresh)
        {
            return Read(session, context.ListTab, list.ReferenceName, list.Title, refresh: true);
        }

        return Entry(session, list, pressed);
    }

    private static ReflogUpdate Entry(ReflogSession session, ReflogState list, ReflogActionPressed pressed)
    {
        var action = pressed.Action;
        var kind = KindOf(action);
        if (kind is not { } entryKind)
        {
            return Move(session, list, pressed);
        }

        if (list.Selected is not { } entry)
        {
            return Warn(session, NoEntryNotice);
        }

        return Done(session, new ReflogEntryActionEffect(list.TabId, entryKind, entry.Hash));
    }

    private static ReflogEntryKind? KindOf(ActionId action)
    {
        if (action == ActionIds.ReflogSelectCommit)
        {
            return ReflogEntryKind.SelectCommit;
        }

        if (action == ActionIds.ResetBranch)
        {
            return ReflogEntryKind.Reset;
        }

        if (action == ActionIds.BranchCreate)
        {
            return ReflogEntryKind.CreateBranch;
        }

        return action == ActionIds.BranchSwitch ? ReflogEntryKind.CheckoutDetached : null;
    }

    private static ReflogUpdate Move(ReflogSession session, ReflogState list, ReflogActionPressed pressed)
    {
        var rows = Math.Max(1, pressed.PageRows);
        var moved = ListMotion.TryMove(
            pressed.Action,
            pressed.Count,
            list.SelectedIndex,
            list.Entries.Count,
            rows,
            out var target);
        if (moved)
        {
            return With(session, list.MoveTo(target));
        }

        return Done(session);
    }

    private static ReflogUpdate InPicker(
        ReflogSession session,
        ReflogState list,
        ReflogRefPicker picker,
        ActionId action,
        ReflogContext context)
    {
        if (action == ActionIds.InputAccept)
        {
            return Accept(session, list, picker, context);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return With(session, list with { Picker = null }, new SetKeyModeEffect(KeyMode.Normal));
        }

        return action == ActionIds.AppQuit ? Close(session) : Done(session);
    }

    private static ReflogUpdate InSearchBox(ReflogSession session, ReflogState list, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            return With(session, list.AcceptSearch(), new SetKeyModeEffect(KeyMode.Normal));
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return With(session, list.ClearSearch(), new SetKeyModeEffect(KeyMode.Normal));
        }

        return action == ActionIds.AppQuit ? Close(session) : Done(session);
    }

    private static ReflogUpdate StepMatch(ReflogSession session, ReflogState list, bool forward, int? count)
    {
        if (list.MatchIndexes.Count == 0)
        {
            return Warn(session, list.HasSearch ? $"No match for {list.SearchText}" : NoSearchNotice);
        }

        return With(session, list.StepMatch(forward, count ?? 1));
    }

    private static ReflogUpdate OpenPicker(ReflogSession session, ReflogState list, RepositoryState tab)
    {
        var names = new List<string> { HeadName };
        names.AddRange(tab.Refs
            .Where(reference => reference.Kind == RefKind.LocalBranch && reference.SymbolicTarget is null)
            .Select(reference => reference.Name));

        return With(session, list with { Picker = new ReflogRefPicker(names) }, new SetKeyModeEffect(KeyMode.Insert));
    }

    private static ReflogUpdate Accept(
        ReflogSession session,
        ReflogState list,
        ReflogRefPicker picker,
        ReflogContext context)
    {
        if (picker.Selected is not { } name || context.ListTab is not { } tab)
        {
            return Done(session);
        }

        if (session.IsReading)
        {
            return Warn(session, StillReadingNotice);
        }

        var isHead = name == HeadName;
        var reference = isHead ? HeadName : $"{GitRefPrefixes.Heads}{name}";
        var read = Read(session with { List = list with { Picker = null } }, tab, reference, name, refresh: false);

        return read with { Effects = [new SetKeyModeEffect(KeyMode.Normal), .. read.Effects] };
    }

    // The list opens (or updates) only if nothing was opened or closed meanwhile: the result echoes the generation.
    private static ReflogUpdate Read(
        ReflogSession session,
        RepositoryState tab,
        string reference,
        string title,
        bool refresh)
    {
        if (session.IsReading)
        {
            return Done(session);
        }

        var generation = session.Generation + 1;
        var effect = new ReadReflogEffect(tab.Id, tab.Location, reference, title, generation, refresh);

        return Done(session with { Generation = generation, IsReading = true }, effect);
    }

    private static ReflogUpdate Loaded(ReflogSession session, ReflogLoaded loaded)
    {
        var idle = session with { IsReading = false };
        if (loaded.Generation != session.Generation)
        {
            return Done(idle);
        }

        if (loaded.Refresh && session.List is { } current)
        {
            return Done(idle with { List = current.WithEntries(loaded.Entries) });
        }

        var list = new ReflogState(loaded.TabId, loaded.Reference, loaded.Title, loaded.Entries);

        return Done(idle with { List = list });
    }

    private static ReflogUpdate Failed(ReflogSession session, string text) =>
        Done(session with { IsReading = false }, new PostStatusEffect(text, StatusSeverity.Error));

    private static ReflogUpdate TypeKey(ReflogSession session, KeyEvent key, ReflogContext context)
    {
        if (!session.TakesText(context.ActiveTab?.Id) || session.List is not { } list)
        {
            return Done(session);
        }

        if (list.Picker is { } picker)
        {
            var normalized = key.Normalize();
            if (normalized is { Name: KeyNames.Up or KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
            {
                return With(session, list with { Picker = picker.Move(normalized.Name == KeyNames.Up ? -1 : 1) });
            }

            if (PromptTextEditor.Apply(picker.Text, key) is { } text && text != picker.Text)
            {
                return With(session, list with { Picker = picker.WithText(text) });
            }

            return Done(session);
        }

        if (PromptTextEditor.Apply(list.SearchText, key) is { } edited && edited != list.SearchText)
        {
            return With(session, list.EditSearch(edited));
        }

        return Done(session);
    }

    private static ReflogUpdate ClickEntry(ReflogSession session, int index)
    {
        if (session.List is { Picker: null } list)
        {
            return With(session, list.MoveTo(index));
        }

        return Done(session);
    }

    private static ReflogUpdate ClickPickerRow(ReflogSession session, int index)
    {
        if (session.List is { Picker: { } picker } list)
        {
            return With(session, list with { Picker = picker.MoveTo(index) });
        }

        return Done(session);
    }

    private static ReflogUpdate Close(ReflogSession session)
    {
        var closed = session with { List = null, Generation = session.Generation + 1 };
        if (session.List is { IsEditingSearch: true } or { Picker: not null })
        {
            return Done(closed, new SetKeyModeEffect(KeyMode.Normal));
        }

        return Done(closed);
    }

    private static ReflogUpdate Warn(ReflogSession session, string text) =>
        Done(session, new PostStatusEffect(text, StatusSeverity.Warning));

    private static ReflogUpdate With(ReflogSession session, ReflogState list, params AppEffect[] effects) =>
        Done(session with { List = list }, effects);

    private static ReflogUpdate Done(ReflogSession session, params AppEffect[] effects) =>
        new(session, [.. effects]);
}
