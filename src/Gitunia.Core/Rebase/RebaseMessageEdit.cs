using Gitunia.Core.Editing;

namespace Gitunia.Core.Rebase;

/// <summary>The open message editor of a reword: whose message it edits and the editor holding the text.</summary>
/// <param name="Hash">The full hash of the commit being reworded.</param>
/// <param name="Editor">The multi-line editor with the message so far.</param>
public sealed record RebaseMessageEdit(string Hash, MessageEditor Editor);
