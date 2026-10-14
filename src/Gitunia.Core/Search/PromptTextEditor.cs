using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Search;

/// <summary>Single-line text editing for prompts in Insert mode: typed text, <c>Backspace</c>, <c>Ctrl-u</c> (clear) and <c>Ctrl-w</c> (delete word).</summary>
public static class PromptTextEditor
{
    /// <summary>Applies a key to the text; returns null when the key does not edit text.</summary>
    public static string? Apply(string text, KeyEvent rawKey)
    {
        var key = rawKey.Normalize();
        if (key.IsPrintable)
        {
            return text + key.Name;
        }

        if (key is { Name: KeyNames.Backspace, Modifiers: KeyModifiers.None })
        {
            if (text.Length == 0)
            {
                return text;
            }

            var cut = text.Length >= 2 && char.IsLowSurrogate(text[^1]) && char.IsHighSurrogate(text[^2]) ? 2 : 1;
            return text[..^cut];
        }

        if (key is { Name: "u", Modifiers: KeyModifiers.Ctrl })
        {
            return string.Empty;
        }

        if (key is { Name: "w", Modifiers: KeyModifiers.Ctrl })
        {
            var trimmed = text.TrimEnd(' ');
            var lastSpace = trimmed.LastIndexOf(' ');
            return lastSpace < 0 ? string.Empty : trimmed[..(lastSpace + 1)];
        }

        return null;
    }
}
