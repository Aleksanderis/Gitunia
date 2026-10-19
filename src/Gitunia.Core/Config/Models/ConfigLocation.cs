namespace Gitunia.Core.Config.Models;

/// <summary>The outcome of looking up a path in the config text.</summary>
/// <param name="Kind">What was found.</param>
/// <param name="Span">The value, quotes included for a string (Found only).</param>
/// <param name="Container">The object that lacks the first missing segment, braces included (Absent only).</param>
/// <param name="ResolvedDepth">
/// How many leading path segments exist (Absent: the container is that deep; ParentNotObject: the value at this depth
/// is not an object, 0 is the root).
/// </param>
/// <param name="Line">The 1-based line of the syntax error (Invalid only).</param>
/// <param name="Message">The reason of the syntax error (Invalid only).</param>
/// <param name="Shadowed">True when a member on the way was written more than once (the last one is used).</param>
public sealed record ConfigLocation(
    ConfigLocationKind Kind,
    TextSpan Span = default,
    TextSpan? Container = null,
    int ResolvedDepth = 0,
    int Line = 0,
    string Message = "",
    bool Shadowed = false);
