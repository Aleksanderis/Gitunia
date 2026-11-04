using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform;
using Gitunia.Tui.App.Blame;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.App.Blame;
using Gitunia.Tui.Tests.App.Panes;
using Gitunia.Tui.Tests.Fakes;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Clipboard;

/// <summary>A selection the pointer finished is copied when the setting is on, and only then.</summary>
public sealed class CopyOnSelectTests : IDisposable
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly List<AppEffect> _effects = [];
    private readonly ListView _list;
    private readonly RangeSource _source = new(20);
    private readonly VisualRangeInput _visual;
    private readonly LiveConfig _config;

    public CopyOnSelectTests()
    {
        _list = new ListView { Source = _source };
        var root = Path.Combine(Path.GetTempPath(), "gitunia-copy-on-select");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
        _host.Dispatch(new OpenRepositoryCommand(location));
        _host.Dispatch(new TabCommand(_host.Workspace.ActiveTabId!.Value, new FocusPaneCommand(PaneKind.Graph)));
        _host.EffectRaised += _effects.Add;
        _visual = new VisualRangeInput(_host, new FakePaneLists(_list));
        _config = new LiveConfig(
            new ConfigStartup(Path.Combine(Path.GetTempPath(), "gitunia-no-config"), Path.GetTempPath()).Load(),
            new ConsoleCapabilities(false, ColorDepth.None, true),
            Path.GetTempPath());
        var blame = new BlameInput(_host, new BlameStore(), new NoBlameTargets(), GraphGlyphSet.Unicode);
        _ = new CopyOnSelect(_host, _visual, blame, _config); // Wires itself to the pointer events.
    }

    public void Dispose()
    {
        _list.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void GivenTheDefaultSettings_WhenADragIsReleased_ThenTheSelectionIsCopiedAndKept()
    {
        StartSelection();

        _visual.OnDragReleased();

        Assert.Equal(VisualCopyMode.OnSelect, Assert.Single(_effects.OfType<CopyVisualRangeEffect>()).Mode);
        Assert.True(_visual.IsActive);
    }

    [Fact]
    public void GivenCopyOnSelectIsOff_WhenADragIsReleased_ThenNothingIsCopied()
    {
        _config.ChangeOptions(_config.Options with { CopyOnSelect = false });
        StartSelection();

        _visual.OnDragReleased();

        Assert.Empty(_effects.OfType<CopyVisualRangeEffect>());
    }

    [Fact]
    public void GivenASelectionMadeWithKeys_WhenItChanges_ThenNothingIsCopiedByItself()
    {
        StartSelection();

        Assert.Empty(_effects.OfType<CopyVisualRangeEffect>());
    }

    private void StartSelection()
    {
        _list.SelectedItem = 4;
        _visual.TryExecute(ActionIds.VisualToggle, null);
    }
}
