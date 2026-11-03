using Gitunia.Core.Editing;

namespace Gitunia.Core.Jump.Models;

/// <summary>What one key did to the quick jump prompt.</summary>
/// <param name="Text">The typed text after the key.</param>
/// <param name="JumpRow">The row to jump to when the key was a label, null otherwise.</param>
public readonly record struct QuickJumpUpdate(string Text, int? JumpRow, int Caret = PromptCaret.End);
