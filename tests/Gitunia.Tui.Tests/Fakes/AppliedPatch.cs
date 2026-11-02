using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>A patch the <see cref="FakeIndexWriter"/> was asked to apply.</summary>
/// <param name="Text">The patch text.</param>
/// <param name="Direction">Forward (stage) or reverse (unstage).</param>
// ReSharper disable NotAccessedPositionalProperty.Global : Fake shared with Gitunia.App.Tests by link, a member counts as unused when only the other project uses it
public sealed record AppliedPatch(string Text, PatchDirection Direction);
