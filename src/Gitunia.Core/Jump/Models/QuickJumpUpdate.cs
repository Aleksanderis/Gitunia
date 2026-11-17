using Gitunia.Core.Editing;

namespace Gitunia.Core.Jump.Models;

/// <summary>What one key did to the quick jump prompt.</summary>
/// <param name="Field">The typed text after the key and its caret.</param>
/// <param name="JumpRow">The row to jump to when the key was a label, null otherwise.</param>
public readonly record struct QuickJumpUpdate(TextField Field, int? JumpRow);
