using Gitunia.Tui.Input;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The text of a prompt's popup as one string for assertions; empty when no popup is open.</summary>
public static class PopupText
{
    public static string Of(IPromptDisplay prompt)
    {
        if (prompt.Popup is not { } popup)
        {
            return string.Empty;
        }

        var input = popup.Input is { } text ? $"> {text}_" : string.Empty;
        return string.Join('\n', [popup.Title, .. popup.Lines, input, .. popup.Details, popup.Hint]);
    }
}
