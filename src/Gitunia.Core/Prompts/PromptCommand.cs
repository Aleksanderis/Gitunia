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

/// <summary>Esc in the open prompt, or its tab was left: it closes and nothing else happens.</summary>
public sealed record PromptCancelledCommand : PromptCommand;
