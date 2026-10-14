using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Tests.Keymap;

/// <summary>Test helper that pushes keys written in documentation notation through an engine and keeps the state.</summary>
public sealed class KeyFeeder(KeymapEngine engine, string view = DefaultKeymap.GraphView)
{
    public KeymapState State { get; set; } = KeymapState.Initial;

    public string View { get; set; } = view;

    public List<KeyResult> Results { get; } = [];

    public KeyResult Last => Results[^1];

    public KeyResult Feed(string keys)
    {
        foreach (var key in KeySequenceParser.Parse(keys).Keys)
        {
            Feed(key);
        }

        return Last;
    }

    public KeyResult Feed(KeyEvent key)
    {
        var result = engine.Process(State, key, View);
        State = result.State;
        Results.Add(result);
        return result;
    }
}
