using Gitunia.Core.Workspaces;

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
        TypeTextCommand type => WithQuery(state, state.Query + type.Text, context),
        BackspaceCommand => WithQuery(state, RemoveLastCharacter(state.Query), context),
        ClearQueryCommand => WithQuery(state, string.Empty, context),
        MoveHighlightCommand move => state with { Highlight = Math.Clamp(state.Highlight + move.Delta, 0, Math.Max(0, state.Rows.Length - 1)) },
        CompletePathCommand => state.IsPathMode && !state.IsListing ? WithQuery(state, PathCompleter.Complete(state.Query, state.Rows, state.Highlight), context) : state,
        SetMessageCommand message => state with { Message = message.Message },
        ListingCompletedCommand listing => listing.Query == state.Query && state.IsPathMode
            ? state with { Rows = listing.Rows, Highlight = 0, Message = listing.Message.Length > 0 ? listing.Message : state.Message, IsListing = false }
            : state,
        RefreshRowsCommand => Refresh(state, context),
        _ => state,
    };

    // A path is listed in the background (the controller starts it when IsListing turns on), so the reducer never touches the file system.
    private static PickerState WithQuery(PickerState state, string query, PickerContext context)
    {
        var isPath = PathQuery.IsPath(query);
        return state with { Query = query, Rows = isPath ? [] : PickerRowBuilder.Build(query, context), Highlight = 0, Message = string.Empty, IsListing = isPath };
    }

    private static PickerState Refresh(PickerState state, PickerContext context)
    {
        if (state.IsPathMode)
        {
            return state;
        }

        var rows = PickerRowBuilder.Build(state.Query, context);
        var highlighted = state.HighlightedRow;
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
        return state with { Rows = rows, Highlight = highlight };
    }

    private static string RemoveLastCharacter(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return char.IsLowSurrogate(text[^1]) && text.Length >= 2 ? text[..^2] : text[..^1];
    }
}
