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
