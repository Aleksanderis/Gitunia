using Gitunia.Core.Editing;
using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Input;

/// <summary>What an open prompt shows: a line in the status bar (find-as-you-type bars) or a centred popup.</summary>
public interface IPromptDisplay
{
    /// <summary>What the prompt shows in the status bar while it is open (the typed text with its caret), empty when it is not.</summary>
    PromptLine StatusLine { get; }

    /// <summary>The prompt as a centred popup while it asks for text or a choice, null when closed or a bar.</summary>
    PromptPopup? Popup => null;
}
