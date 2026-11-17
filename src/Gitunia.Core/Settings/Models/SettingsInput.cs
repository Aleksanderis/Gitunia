using Gitunia.Core.Editing;

namespace Gitunia.Core.Settings.Models;

/// <summary>Text being typed into a setting (Insert mode).</summary>
/// <param name="Kind">What the text is for.</param>
/// <param name="SettingId">The setting it belongs to.</param>
/// <param name="ItemIndex">The folder being changed for <see cref="SettingsInputKind.PathEdit"/>, else -1.</param>
/// <param name="Field">What was typed so far and the caret.</param>
public sealed record SettingsInput(SettingsInputKind Kind, string SettingId, int ItemIndex, TextField Field)
{
    /// <summary>A box that opens with <paramref name="text"/> and the caret at its end.</summary>
    public SettingsInput(SettingsInputKind kind, string settingId, int itemIndex, string text)
        : this(kind, settingId, itemIndex, TextField.Of(text))
    {
    }

    /// <summary>What was typed so far.</summary>
    public string Text => Field.Text;

    /// <summary>The caret column in <see cref="Text"/> (see <see cref="PromptCaret"/>).</summary>
    public int Caret => Field.Caret;
}
