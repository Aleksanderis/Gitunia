using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Effects;

/// <summary>Offer to run a git command again in the real terminal after a background run failed for lack of
/// credentials (#512).</summary>
/// <param name="TabId">The tab the command belongs to.</param>
/// <param name="Noun">What failed, for the dialog (such as <c>Tag push</c>).</param>
/// <param name="Arguments">The git arguments of the same command.</param>
public sealed record OfferTerminalRetryEffect(TabId TabId, string Noun, IReadOnlyList<string> Arguments) : AppEffect;
