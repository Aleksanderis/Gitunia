using Gitunia.Core.Editing;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Jump;

/// <summary>The open quick jump prompt: it belongs to the tab and the pane it was opened in.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Pane">The pane whose list is labelled.</param>
/// <param name="Text">The text typed so far.</param>
/// <param name="Caret">The caret column in <see cref="Text"/> (see <see cref="PromptCaret"/>).</param>
public sealed record QuickJumpSession(TabId TabId, PaneKind Pane, string Text, int Caret = PromptCaret.End);
