namespace Gitunia.Git.IssueTracker.Models;

/// <summary>One <c>[issuetracker "name"]</c> section of the <c>.issuetracker</c> file.</summary>
/// <param name="Name">The subsection name.</param>
/// <param name="Pattern">The <c>regex</c> value: text matching it becomes a link; group 1 is the reference.</param>
/// <param name="UrlTemplate">The <c>url</c> value; <c>$1</c> stands for the reference.</param>
public sealed record IssueTrackerRule(string Name, string Pattern, string UrlTemplate);
