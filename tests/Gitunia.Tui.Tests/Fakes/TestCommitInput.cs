using Gitunia.App;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Builds the commit message input for tests whose subject is another part of the Changes view.</summary>
public static class TestCommitInput
{
    public static CommitMessageInput Create(Dispatcher host) => new(host, new RecordingRoute());

    /// <summary>Records the actions the input passes on, as the application would route them.</summary>
    public sealed class RecordingRoute : IActionRoute
    {
        public List<ActionId> Routed { get; } = [];

        public void Route(ActionId action, int? count) => Routed.Add(action);
    }
}
