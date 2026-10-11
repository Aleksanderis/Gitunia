namespace Gitunia.Core.Clipboard;

/// <summary>How an OSC 52 sequence has to be delivered to the terminal.</summary>
public enum Osc52Mode
{
    /// <summary>Send the sequence as is.</summary>
    Direct,

    /// <summary>Inside tmux: wrap in a tmux passthrough (needs <c>allow-passthrough on</c> or <c>set-clipboard on</c>).</summary>
    Tmux,

    /// <summary>Inside GNU screen: wrap in DCS chunks.</summary>
    Screen,

    /// <summary>The terminal is known not to support OSC 52.</summary>
    Unsupported,
}
