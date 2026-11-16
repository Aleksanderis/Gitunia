using Gitunia.Tui.Input;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// The text of a prompt's popup as one string for assertions; empty when no popup is open. The highlighted row is
/// written with a <c>&gt; </c> prefix and the count after the title, so a test can name both in text.
/// </summary>
public static class PopupText
{
    public static string Of(IPromptDisplay prompt)
    {
        if (prompt.Popup is not { } popup)
        {
            return string.Empty;
        }

        var input = popup.Input is { } text ? $"> {text}_" : string.Empty;
        var title = popup.Count is { } count ? $"{popup.Title} {count}" : popup.Title;
        var rows = popup.Details.Select((row, index) => index == popup.Highlight ? $"> {row}" : $"  {row}");
        return string.Join('\n', [title, .. popup.Lines, input, .. rows, popup.Hint]);
    }
}
