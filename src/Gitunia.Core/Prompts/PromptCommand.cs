using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;

namespace Gitunia.Core.Prompts;

/// <summary>A command on the open prompt: open, type or move, accept, cancel. Reduced by <see cref="PromptReducer"/>.</summary>
public abstract record PromptCommand : ShellCommand;

/// <summary>
/// Open a prompt (Insert mode). Ignored while another prompt is open, so a late result cannot replace what the user is
/// typing. A dialog or output box that is open is replaced.
/// </summary>
/// <param name="Request">What to ask.</param>
public sealed record PromptRequestedCommand(PromptRequest Request) : PromptCommand;

/// <summary>A key typed in the open prompt: text editing, or the move keys of a picker.</summary>
/// <param name="Key">The key.</param>
public sealed record PromptKeyPressedCommand(KeyEvent Key) : PromptCommand;

/// <summary>
/// Enter in the open prompt: it closes and its result is reduced. A picker with no highlighted candidate stays open and
/// says so.
/// </summary>
public sealed record PromptAcceptedCommand : PromptCommand;

/// <summary>
/// A feature rejected the answer of the prompt that just closed: it opens again with what was typed and the status line
/// says why (<see cref="ReopenPromptEffect"/>). Ignored when another prompt is open or nothing was answered.
/// </summary>
/// <param name="Reason">Why the answer was rejected.</param>
public sealed record PromptReopenedCommand(string Reason) : PromptCommand;

/// <summary>Esc in the open prompt, or its tab was left: it closes and nothing else happens.</summary>
public sealed record PromptCancelledCommand : PromptCommand;

/// <summary>
/// The highlight of the open picker prompt moves by <paramref name="Step"/> rows, stopping at the ends: the wheel, a click
/// on a row (relative to the highlighted one) and the move hints of the popup. Ignored by a prompt without candidates.
/// </summary>
/// <param name="Step">Rows down (positive) or up (negative).</param>
public sealed record PromptMovedCommand(int Step) : PromptCommand;

/// <summary>A click on an option of a one-key prompt: it answers like the option's key. Ignored for any other prompt.</summary>
/// <param name="Index">The option, from 0, in the order the popup lists them.</param>
public sealed record PromptChoiceSelectedCommand(int Index) : PromptCommand;
