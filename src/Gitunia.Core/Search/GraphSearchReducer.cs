using System.Collections.Immutable;
using Gitunia.Core.Search.Models;

namespace Gitunia.Core.Search;

/// <summary>
/// Pure transitions of a tab's graph search: <c>(state, command) -> (state, effects)</c>. Scanning is done by
/// whoever runs the <see cref="ScanSearchEffect"/> (see <see cref="GraphSearchScanner"/>) and reported back as
/// <see cref="ScanProgressCommand"/>; a result of an older generation is ignored, which is how a scan is cancelled.
/// </summary>
public static class GraphSearchReducer
{
    public const string WrappedNotice = "Search wrapped";
    public const string NoSearchNotice = "No search yet; press / to start one";
    public const string NoMatchNotice = "No match";

    public static GraphSearchUpdate Reduce(GraphSearchState state, GraphSearchCommand command) => command switch
    {
        OpenSearchCommand => Plain(GraphSearchState.Initial with { Phase = SearchPhase.Editing, Generation = state.Generation + 1 }),
        EditSearchTextCommand edit => Edit(state, edit.Text),
        AcceptSearchCommand accept => Accept(state, accept.SelectedIndex),
        CancelSearchCommand => Plain(GraphSearchState.Initial with { Generation = state.Generation + 1 }),
        StepMatchCommand step => Step(state, step),
        ScanProgressCommand progress => Progress(state, progress),
        GraphGrewCommand grew => Grew(state, grew),
        GraphReplacedCommand => Replaced(state),
        _ => Plain(state),
    };

    private static GraphSearchUpdate Plain(GraphSearchState state) => new(state, []);

    private static GraphSearchUpdate Edit(GraphSearchState state, string text)
    {
        if (state.Phase != SearchPhase.Editing)
        {
            return Plain(state);
        }

        var query = SearchQueryParser.Parse(text);
        var next = state with
        {
            Text = text,
            Query = query,
            Matches = [],
            Scanned = 0,
            IsScanning = query is not null,
            IsComplete = query is null,
            Generation = state.Generation + 1,
            JumpPending = false,
            Notice = string.Empty,
        };
        return new GraphSearchUpdate(next, query is null ? [] : [new ScanSearchEffect(next.Generation)]);
    }

    private static GraphSearchUpdate Accept(GraphSearchState state, int selectedIndex)
    {
        if (state.Phase != SearchPhase.Editing)
        {
            return Plain(state);
        }

        if (state.Query is null)
        {
            return Plain(GraphSearchState.Initial with { Generation = state.Generation + 1 });
        }

        // The jump goes to the first match after the anchor, so the selected row itself counts.
        return Seek(state with { Phase = SearchPhase.Active, Anchor = selectedIndex - 1, Notice = string.Empty });
    }

    private static GraphSearchUpdate Step(GraphSearchState state, StepMatchCommand step)
    {
        if (state.Phase != SearchPhase.Active || state.Query is null)
        {
            return Plain(state with { Notice = NoSearchNotice });
        }

        var matches = state.Matches;
        var steps = Math.Max(1, step.Count);
        var cleared = state with { Notice = string.Empty, Anchor = step.SelectedIndex };
        if (step.Forward)
        {
            var target = UpperBound(matches, step.SelectedIndex) + steps - 1;
            if (target < matches.Length)
            {
                return Reveal(cleared, matches[target]);
            }

            if (!state.IsComplete)
            {
                return Plain(cleared with { JumpPending = true });
            }

            return matches.IsEmpty ? Plain(cleared with { Notice = NoMatchNotice }) : Reveal(cleared with { Notice = WrappedNotice }, matches[target % matches.Length]);
        }

        var previous = LowerBound(matches, step.SelectedIndex) - 1 - (steps - 1);
        if (previous >= 0)
        {
            return Reveal(cleared, matches[previous]);
        }

        if (matches.IsEmpty)
        {
            return Plain(cleared with { Notice = state.IsComplete ? NoMatchNotice : string.Empty });
        }

        var wrapped = ((previous % matches.Length) + matches.Length) % matches.Length;
        return Reveal(cleared with { Notice = WrappedNotice }, matches[wrapped]);
    }

    private static GraphSearchUpdate Progress(GraphSearchState state, ScanProgressCommand progress)
    {
        if (progress.Generation != state.Generation || state.Query is null)
        {
            return Plain(state);
        }

        var scanning = progress.NextIndex < progress.LoadedCount;
        var next = state with
        {
            Matches = progress.Matches.IsEmpty ? state.Matches : state.Matches.AddRange(progress.Matches),
            Scanned = progress.NextIndex,
            IsScanning = scanning,
            IsComplete = !scanning && progress.LoadComplete,
        };
        var update = next.JumpPending && next.Phase == SearchPhase.Active ? Seek(next) : Plain(next);
        return scanning ? update with { Effects = [.. update.Effects, new ScanSearchEffect(next.Generation)] } : update;
    }

    private static GraphSearchUpdate Grew(GraphSearchState state, GraphGrewCommand grew)
    {
        if (state.Query is null || state.IsScanning)
        {
            return Plain(state);
        }

        if (state.Scanned < grew.LoadedCount)
        {
            return new GraphSearchUpdate(state with { IsScanning = true, IsComplete = false }, [new ScanSearchEffect(state.Generation)]);
        }

        return grew.LoadComplete && !state.IsComplete
            ? Progress(state, new ScanProgressCommand(state.Generation, [], state.Scanned, grew.LoadedCount, true))
            : Plain(state);
    }

    private static GraphSearchUpdate Replaced(GraphSearchState state)
    {
        if (state.Query is null)
        {
            return Plain(state);
        }

        var next = state with { Matches = [], Scanned = 0, IsScanning = true, IsComplete = false, Generation = state.Generation + 1, JumpPending = false };
        return new GraphSearchUpdate(next, [new ScanSearchEffect(next.Generation)]);
    }

    // Jumps to the first match after the anchor; waits for the scan when there is none yet and it is not finished.
    private static GraphSearchUpdate Seek(GraphSearchState state)
    {
        var index = UpperBound(state.Matches, state.Anchor);
        if (index < state.Matches.Length)
        {
            return Reveal(state, state.Matches[index]);
        }

        if (!state.IsComplete)
        {
            return Plain(state with { JumpPending = true });
        }

        return state.Matches.IsEmpty
            ? Plain(state with { JumpPending = false, Notice = NoMatchNotice })
            : Reveal(state with { Notice = WrappedNotice }, state.Matches[0]);
    }

    private static GraphSearchUpdate Reveal(GraphSearchState state, int index) =>
        new(state with { JumpPending = false }, [new RevealMatchEffect(index)]);

    // First position whose value is greater than the given one.
    private static int UpperBound(ImmutableArray<int> values, int value)
    {
        var low = 0;
        var high = values.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (values[middle] > value)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    // First position whose value is at least the given one.
    private static int LowerBound(ImmutableArray<int> values, int value) => UpperBound(values, value - 1);
}
