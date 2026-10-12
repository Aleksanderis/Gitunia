using Gitunia.Core.Diff;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
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
        CancellationToken cancellationToken,
        PaneArrangement? arrangement = null)
    {
        var graph = new CommitGraph();
        var repo = new Repo(runner, location);
        var wanted = ScreenLayout.GraphHeight(height);
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, wanted, cancellationToken))
        {
            graph.AddRange(batch);
        }

        var head = await repo.ReadHeadAsync(cancellationToken);
        var selected = graph.Count > 0 ? graph[0] : null;
        var detailState = CommitDetailsState.Empty;
        if (selected is not null && DetailLayout.PaneHeight(height) > 0)
        {
            var details = await CommitDetailsReader.ReadAsync(repo, selected.Id, cancellationToken);
            detailState = details is null
                ? new CommitDetailsState(selected.Id, CommitDetailsStatus.Failed, null, "Commit not found")
                : new CommitDetailsState(selected.Id, CommitDetailsStatus.Loaded, details, string.Empty);
        }

        var diff = arrangement is { IsDiffOpen: true } && arrangement.Zoomed is null or PaneKind.Diff && selected is not null
            ? await ReadDiffAsync(repo, selected, detailState, cancellationToken)
            : null;
        var detail = new DetailPaneModel(DetailContentBuilder.Build(selected, detailState), 0, 0, false);
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
        var sidebar = await ReadSidebarAsync(repo, location, head, cancellationToken);
        foreach (var line in ScreenComposer.Compose(model, graph, ScreenLayout.ReservedLeadingRows, 0, width, height, glyphs, detail, sidebar, null, diff, diff is null ? null : arrangement))
        {
            await output.WriteLineAsync(line.Text.AsMemory(), cancellationToken);
        }
    }

    private static async Task<DiffPaneModel> ReadDiffAsync(Repo repo, GraphEntry selected, CommitDetailsState detailState, CancellationToken cancellationToken)
    {
        // The diff needs the parents; the detail pane is not read when the terminal is too short for it.
        var details = detailState.Details ?? await CommitDetailsReader.ReadAsync(repo, selected.Id, cancellationToken);
        var firstParent = details is { Parents.Count: > 0 } ? details.Parents[0] : (ObjectId?)null;
        var builder = new DiffDocumentBuilder();
        var warnings = new List<string>();
        await foreach (var file in DiffReader.ReadCommitAsync(repo, selected.Id, firstParent, cancellationToken, warnings, DiffStore.DefaultFileLineLimit))
        {
            builder.Add(file);
        }

        return new DiffPaneModel(builder.Build(warnings), selected.Id.ToString()[..7], 0, 0, false);
    }

    private static async Task<SidebarModel> ReadSidebarAsync(Repo repo, RepositoryLocation location, HeadState head, CancellationToken cancellationToken)
    {
        var refs = await RefReader.ReadRefsAsync(repo, cancellationToken);
        var worktrees = await RefReader.ReadWorktreesAsync(repo, cancellationToken);
        var stashes = await StashReader.ReadStashesAsync(repo, cancellationToken);
        var tab = RepoState.Create(new TabId(1), location);
        foreach (var command in new RepoCommand[]
        {
            new HeadLoadedCommand(head),
            new RefsLoadedCommand([.. refs.Items]),
            new WorktreesLoadedCommand([.. worktrees.Items]),
            new StashesLoadedCommand([.. stashes.Items]),
        })
        {
            tab = RepoReducer.Reduce(tab, command);
        }

        return new SidebarModel(SidebarBuilder.Build(tab), 0, 0, string.Empty, false, false);
    }
}
