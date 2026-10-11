using Gitunia.Core.Clipboard;

namespace Gitunia.Tui;

/// <summary>Runs <see cref="CopyToClipboardEffect"/>s: the only place that writes OSC 52 to the terminal.</summary>
public sealed class ClipboardWriter
{
    private readonly TextWriter output;
    private readonly Osc52Options options;

    public ClipboardWriter(TextWriter output, Osc52Options options)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        this.output = output;
        this.options = options;
    }

    public ClipboardOutcome Run(CopyToClipboardEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        var result = Osc52Encoder.Encode(effect.Text, options);
        if (result.Status != Osc52Status.Ready)
        {
            return new ClipboardOutcome(false, result.Message);
        }

        try
        {
            output.Write(result.Sequence);
            output.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return new ClipboardOutcome(false, $"Cannot copy: writing to the terminal failed ({exception.Message}).");
        }

        return new ClipboardOutcome(true, $"Copied {effect.Description} (sent to the terminal; if the clipboard is unchanged, enable OSC 52 in your terminal or tmux).");
    }
}
