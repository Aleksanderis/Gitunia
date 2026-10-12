namespace Gitunia.Core.Overlays;

/// <summary>One line of the help overlay.</summary>
/// <param name="Keys">The bindings of the action (<c>j / Down</c>).</param>
/// <param name="Description">What the action does.</param>
public sealed record HelpRow(string Keys, string Description);
