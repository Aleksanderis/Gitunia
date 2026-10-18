using Gitunia.Tui.Input;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The text a prompt's popup shows, flattened to one string for assertions; empty when no popup is open.</summary>
public static class PopupText
{
    public static string Of(IPromptHandler prompt)
    {
        if (prompt.Popup is not { } popup)
        {
            return string.Empty;
        }

        var input = popup.Input is { } text ? $"> {text}_" : string.Empty;
        return string.Join('\n', [popup.Title, .. popup.Lines, input, .. popup.Details, popup.Hint]);
    }
}
