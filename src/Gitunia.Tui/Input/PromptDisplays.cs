using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Input;

/// <summary>The text and choice prompts that can be open, in the order their popup wins.</summary>
/// <param name="displays">The prompts.</param>
public sealed class PromptDisplays(IReadOnlyList<IPromptDisplay> displays)
{
    /// <summary>The prompts that want to know when the loaded graph changes.</summary>
    public IEnumerable<IGraphObserver> GraphObservers => displays.OfType<IGraphObserver>();

    /// <summary>The popup of the open prompt, null when none shows one.</summary>
    public PromptPopup? CurrentPopup() =>
        displays.Select(prompt => prompt.Popup).FirstOrDefault(popup => popup is not null);

    /// <summary>What the open prompt shows in the status bar, empty when none does.</summary>
    public string StatusLine() =>
        displays.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ?? string.Empty;
}
