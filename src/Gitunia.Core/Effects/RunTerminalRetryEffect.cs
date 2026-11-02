using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Effects;

/// <summary>Run a git command again in the real terminal after the user accepted the retry dialog (#512).</summary>
/// <param name="TabId">The tab the command belongs to.</param>
/// <param name="Noun">What failed, for the status bar (such as <c>Tag push</c>).</param>
/// <param name="Arguments">The git arguments of the same command.</param>
public sealed record RunTerminalRetryEffect(TabId TabId, string Noun, IReadOnlyList<string> Arguments) : AppEffect;
