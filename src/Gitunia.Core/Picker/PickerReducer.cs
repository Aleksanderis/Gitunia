using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Picker;

/// <summary>Pure transitions of the open picker: <c>(PickerState, PickerCommand) -> PickerState</c>. File system questions come from the <see cref="PickerContext"/>.</summary>
public static class PickerReducer
{
    /// <summary>The state of a freshly opened picker with an empty query.</summary>
    /// <param name="context">The workspace and file system access.</param>
    /// <param name="locatingTabId">The missing tab the chosen repository replaces, null for a normal open.</param>
    /// <param name="initialQuery">Text to start with, such as the parent directory of a missing repository.</param>
    public static PickerState Open(PickerContext context, TabId? locatingTabId = null, string initialQuery = "")
    {
        var isPath = PathQuery.IsPath(initialQuery);
        var rows = isPath ? [] : PickerRowBuilder.Build(initialQuery, context);
        return new PickerState(initialQuery, 0, rows, string.Empty, locatingTabId, isPath);
    }

    public static PickerState Reduce(PickerState state, PickerCommand command, PickerContext context) => command switch
    {
        TypeTextCommand type => Edit(state, PromptTextEditor.Insert(state.Query, state.Caret, type.Text), context),
        BackspaceCommand => Edit(state, PromptTextEditor.Apply(state.Query, state.Caret, new KeyEvent(KeyNames.Backspace)), context),
        EditQueryCommand edit => Edit(state, PromptTextEditor.Apply(state.Query, state.Caret, edit.Key), context),
        ClearQueryCommand => WithQuery(state, string.Empty, context),
        MoveHighlightCommand move => PickerListPlan.Move(state, move.Delta, context.ListBudget),
        SetHighlightCommand set => SetHighlight(state, set),
        ToggleSectionCommand => PickerListPlan.Toggle(state, context.ListBudget),
        CompletePathCommand => Complete(state, context),
        SetMessageCommand message => state with { Message = message.Message },
        ListingCompletedCommand listing => CompleteListing(state, listing),
        ShowScanningCommand scanning => scanning.Query == state.Query && state.IsListing ? state with { IsScanningShown = true } : state,
        RefreshRowsCommand => Refresh(state, context),
        _ => state,
    };

    private static PickerState SetHighlight(PickerState state, SetHighlightCommand set)
    {
        if (set.Row < 0 || set.Row >= state.Rows.Length)
        {
            return state;
        }

        return state with { Highlight = set.Row, IsOnToggle = set.IsOnToggle && !PickerListPlan.IsFlat(state) };
    }

    private static PickerState CompleteListing(PickerState state, ListingCompletedCommand listing)
    {
        if (listing.Query != state.Query || !state.IsPathMode)
        {
            return state;
        }

        var message = state.Message;
        if (listing.Message.Length > 0)
        {
            message = listing.Message;
        }

        return state with
        {
            Rows = listing.Rows,
            Highlight = 0,
            IsOnToggle = false,
            Message = message,
            IsListing = false,
            IsScanningShown = false,
        };
    }

    // A path is listed in the background (the controller starts it when IsListing turns on), so the reducer never touches the file system.
    private static PickerState Edit(PickerState state, PromptEdit? edit, PickerContext context)
    {
        if (edit is not { } change)
        {
            return state;
        }

        // A move of the caret alone keeps the rows and the highlight.
        return change.Text == state.Query ? state with { Caret = change.Caret } : WithQuery(state, change.Text, context, change.Caret);
    }

    private static PickerState WithQuery(PickerState state, string query, PickerContext context, int caret = PromptCaret.End)
    {
        var isPath = PathQuery.IsPath(query);
        if (!isPath)
        {
            var matches = PickerRowBuilder.Build(query, context);
            return state with
            {
                Query = query,
                Caret = caret,
                Rows = matches,
                Highlight = 0,
                IsOnToggle = false,
                Message = string.Empty,
                IsListing = false,
                IsScanningShown = false,
            };
        }

        // Previous path rows stay (and the highlight with them, clamped) until the new listing arrives, so typing does not flicker.
        var rows = state.IsPathMode ? state.Rows : [];
        var highlight = Math.Clamp(state.Highlight, 0, Math.Max(0, rows.Length - 1));
        return state with
        {
            Query = query,
            Caret = caret,
            Rows = rows,
            Highlight = highlight,
            IsOnToggle = false,
            Message = string.Empty,
            IsListing = true,
            IsScanningShown = false,
        };
    }

    // In a path the completion extends the typed name; in a fuzzy query it accepts the highlighted suggestion (its real path, with its own case).
    private static PickerState Complete(PickerState state, PickerContext context)
    {
        if (state.IsPathMode)
        {
            return state.IsListing ? state : WithQuery(state, PathCompleter.Complete(state.Query, state.Rows, state.Highlight), context);
        }

        return state.Query.Length > 0 && state.HighlightedRow is { } row ? WithQuery(state, Path.TrimEndingDirectorySeparator(row.Path) + Path.DirectorySeparatorChar, context) : state;
    }

    private static PickerState Refresh(PickerState state, PickerContext context)
    {
        if (state.IsPathMode)
        {
            return state;
        }

        var rows = PickerRowBuilder.Build(state.Query, context);
        var isInRange = state.Highlight >= 0 && state.Highlight < state.Rows.Length;
        var highlighted = isInRange ? state.Rows[state.Highlight] : null;
        var index = -1;
        for (var candidate = 0; highlighted is not null && candidate < rows.Length; candidate++)
        {
            if (RepositoryPathComparer.Instance.Equals(rows[candidate].Path, highlighted.Path))
            {
                index = candidate;
                break;
            }
        }

        var highlight = index >= 0 ? index : Math.Min(state.Highlight, Math.Max(0, rows.Length - 1));
        var keepsToggle = state.IsOnToggle && index >= 0;
        var refreshed = state with { Rows = rows, Highlight = highlight, IsOnToggle = keepsToggle };

        return PickerListPlan.Normalize(refreshed, context.ListBudget);
    }
}
