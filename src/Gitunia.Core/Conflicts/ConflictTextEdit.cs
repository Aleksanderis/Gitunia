using Gitunia.Core.Editing;

namespace Gitunia.Core.Conflicts;

/// <summary>The open free-text edit of one hunk's result.</summary>
/// <param name="Editor">The multi-line editor holding the text so far.</param>
/// <param name="Original">The editor's text when the edit began; accepting it unchanged changes nothing.</param>
public sealed record ConflictTextEdit(MessageEditor Editor, string Original);
