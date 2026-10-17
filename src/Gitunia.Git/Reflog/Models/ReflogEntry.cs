namespace Gitunia.Git.Reflog.Models;

/// <summary>One line of a reflog, newest first (<paramref name="Index"/> 0 is <c>HEAD@{0}</c>).</summary>
/// <param name="Index">The position, the <c>n</c> of <c>HEAD@{n}</c>.</param>
/// <param name="Hash">The commit the ref pointed to after the action.</param>
/// <param name="Action">
/// What happened: <c>commit</c>, <c>commit (amend)</c>, <c>reset</c>, <c>checkout</c>, <c>merge main</c>...
/// </param>
/// <param name="Message">
/// The rest of git's reflog message (<c>moving from a to b</c>, the commit subject); may be empty.
/// </param>
/// <param name="RelativeDate">When it happened, as git words it (<c>3 hours ago</c>).</param>
public sealed record ReflogEntry(int Index, string Hash, string Action, string Message, string RelativeDate);
