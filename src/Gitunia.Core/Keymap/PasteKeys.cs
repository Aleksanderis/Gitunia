using System.Globalization;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Keymap;

/// <summary>
/// Turns a bracketed paste into the key presses typing it would have produced, so every text input (prompt popups,
/// picker and path filter, search boxes, the message editors) takes a paste through the code it uses for typing.
/// </summary>
public static class PasteKeys
{
    /// <summary>The keys of a paste.</summary>
    /// <param name="text">The pasted text with the bracketing markers already stripped.</param>
    /// <param name="multiLine">
    /// True for a message editor: a line break is an <c>Enter</c> key (a new line). False for a single-line input:
    /// each run of line breaks becomes one space and line breaks at either end are dropped, so a paste never submits.
    /// </param>
    /// <returns>
    /// One key per text element (a combining sequence or an emoji stays whole), a tab is a <c>Tab</c> key in a message editor and a space elsewhere; other control characters are dropped.
    /// </returns>
    public static IReadOnlyList<KeyEvent> FromText(string text, bool multiLine)
    {
        var keys = new List<KeyEvent>();
        var pendingBreak = false;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            if (element is "\r\n" or "\r" or "\n")
            {
                if (multiLine)
                {
                    keys.Add(new KeyEvent(KeyNames.Enter));
                }
                else
                {
                    pendingBreak = keys.Count > 0;
                }

                continue;
            }

            if (element == "\t" && multiLine)
            {
                // The editor decides what a tab is (spaces up to the next stop, or a tab character).
                keys.Add(new KeyEvent(KeyNames.Tab));

                continue;
            }

            if (element == "\t")
            {
                element = " ";
            }

            if (char.IsControl(element[0]))
            {
                continue;
            }

            if (pendingBreak)
            {
                keys.Add(new KeyEvent(" "));
                pendingBreak = false;
            }

            keys.Add(new KeyEvent(element));
        }

        return keys;
    }
}
