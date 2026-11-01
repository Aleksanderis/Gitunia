using System.Collections.Immutable;
using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.IssueLinks;
using Gitunia.Core.IssueTracker.Models;
using Gitunia.Core.Keymap;
using Gitunia.Tui.App.Details;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

public class IssueLinkInputTests
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly FakeView _view = new();
    private readonly List<AppEffect> _effects = [];
    private readonly IssueLinkInput _input;

    public IssueLinkInputTests()
    {
        _input = new IssueLinkInput(_host, _view);
        _host.EffectRaised += _effects.Add;
    }

    [Fact]
    public void GivenTheMessageIsNotFocused_WhenOpening_ThenNothingHappens()
    {
        _view.Focused = false;
        _view.Links = [new IssueReference(0, 2, "https://example.org/1")];

        Assert.False(_input.TryExecute(ActionIds.DetailOpenLink, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenPressingEnter_ThenTheActionIsLeftToTheOthers()
    {
        Assert.False(_input.TryExecute(ActionIds.DetailOpenDiff, null));
        Assert.Empty(_effects);
    }

    [Fact]
    public void GivenALineWithoutLinks_WhenUsingTheOpenKey_ThenItIsHandledAndTheWarningComesOut()
    {
        Assert.True(_input.TryExecute(ActionIds.DetailOpenLink, null));

        Assert.Single(_effects);
    }

    [Fact]
    public void GivenALineWithTwoLinks_WhenPressingEnterWithACount_ThenThatLinkOpens()
    {
        _view.Links =
        [
            new IssueReference(0, 2, "https://example.org/1"),
            new IssueReference(3, 2, "https://example.org/2"),
        ];

        Assert.True(_input.TryExecute(ActionIds.DetailOpenDiff, 2));

        Assert.Contains(_effects, effect => effect is OpenUrlEffect { Url: "https://example.org/2" });
    }

    private sealed class FakeView : IIssueLinkView
    {
        public bool Focused { get; set; } = true;

        public ImmutableArray<IssueReference> Links { get; set; } = [];

        public bool IsMessageFocused() => Focused;

        public ImmutableArray<IssueReference> LinksOfSelectedRow() => Links;
    }
}
