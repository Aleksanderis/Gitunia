namespace Gitunia.Core.Settings.Models;

/// <summary>One line of the key reference of the settings screen: the keys of one action in one view.</summary>
/// <param name="Section">The heading the line is listed under: the view, with the mode when it is not Normal.</param>
/// <param name="Keys">The keys of the action in that view (<c>j / Down</c>).</param>
/// <param name="Title">What the action does.</param>
/// <param name="ActionId">The id of the action, as written in the <c>keys</c> list of the config file.</param>
/// <param name="Snippet">A <c>keys</c> entry of <c>config.json</c> that binds the first of the keys again.</param>
public sealed record KeyReferenceEntry(string Section, string Keys, string Title, string ActionId, string Snippet);
