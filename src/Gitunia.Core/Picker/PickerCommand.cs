using System.Collections.Immutable;

namespace Gitunia.Core.Picker;

/// <summary>A change to the open picker.</summary>
public abstract record PickerCommand
{
    private protected PickerCommand()
    {
    }
}

/// <summary>Text typed into the query.</summary>
/// <param name="Text">The typed characters.</param>
public sealed record TypeTextCommand(string Text) : PickerCommand;

/// <summary>Delete the last character of the query.</summary>
public sealed record BackspaceCommand : PickerCommand;

/// <summary>Clear the query.</summary>
public sealed record ClearQueryCommand : PickerCommand;

/// <summary>Move the highlight; the list does not wrap.</summary>
/// <param name="Delta">Rows to move, negative for up.</param>
public sealed record MoveHighlightCommand(int Delta) : PickerCommand;

/// <summary>Complete the typed path (to the common prefix of the directories, or to the highlighted one).</summary>
public sealed record CompletePathCommand : PickerCommand;

/// <summary>Show a notice below the list.</summary>
/// <param name="Message">The text, empty clears it.</param>
public sealed record SetMessageCommand(string Message) : PickerCommand;

/// <summary>The workspace or the scan results changed (a pin, a removed recent, a finished scan): rebuild the rows and keep the highlighted repository.</summary>
public sealed record RefreshRowsCommand : PickerCommand;

/// <summary>The background listing of a typed path finished (or failed or timed out, then with a message and no rows). Ignored when the query changed meanwhile.</summary>
/// <param name="Query">The query the listing was started for.</param>
/// <param name="Rows">The directory candidates.</param>
/// <param name="Message">A notice such as a timeout, empty when none.</param>
public sealed record ListingCompletedCommand(string Query, ImmutableArray<PickerRow> Rows, string Message = "") : PickerCommand;

/// <summary>The listing of <paramref name="Query"/> is slow enough to show the scanning notice.</summary>
public sealed record ShowScanningCommand(string Query) : PickerCommand;
