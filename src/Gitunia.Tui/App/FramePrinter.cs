using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui.App;

/// <summary>Renders one frame of the graph screen as text without starting the UI (<c>--print-frame</c>): for smoke tests and bug reports.</summary>
public static class FramePrinter
{
    public static async Task PrintAsync(
        GitRunner runner,
        RepositoryLocation location,
        int width,
        int height,
        GraphGlyphSet glyphs,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var graph = new CommitGraph();
        var repo = new Repo(runner, location);
        var wanted = ScreenLayout.GraphHeight(height);
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, wanted, cancellationToken))
        {
            graph.AddRange(batch);
        }

        var head = await repo.ReadHeadAsync(cancellationToken);
        var model = new ScreenModel(
            Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath)),
            head.BranchName,
            1,
            1,
            KeyMode.Normal.ToString().ToUpperInvariant(),
            string.Empty,
            graph.Count > 0 ? 1 : 0,
            graph.Count,
            false,
            string.Empty);
        foreach (var line in ScreenComposer.Compose(model, graph, ScreenLayout.ReservedLeadingRows, 0, width, height, glyphs))
        {
            await output.WriteLineAsync(line.Text.AsMemory(), cancellationToken);
        }
    }
}
