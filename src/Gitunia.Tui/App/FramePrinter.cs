using Gitunia.Core.Diff;
using Gitunia.Core.FileTree;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Theme;

namespace Gitunia.Tui.App;

/// <summary>Renders one frame of the graph screen as text without starting the UI (<c>--print-frame</c>): for smoke tests and bug reports.</summary>
public sealed class FramePrinter
{
    private readonly GitServices _services;

    public FramePrinter(GitServices services)
    {
        _services = services;
    }

    public async Task PrintAsync(
        RepositoryLocation location,
        int width,
        int height,
        GraphGlyphSet glyphs,
        TextWriter output,
        PaneArrangement? arrangement = null,
        BottomTab tab = BottomTab.Changes,
        PaletteSettings? colors = null,
        CancellationToken cancellationToken = default)
    {
        var graph = new CommitGraph();
        var wanted = ScreenLayout.GraphHeight(height);
        await foreach (var batch in _services.GraphLoader.StreamAsync(location, CommitOrder.ByDate, wanted, cancellationToken))
        {
            graph.AddRange(batch);
        }

        var head = await _services.Refs.ReadHeadAsync(location, cancellationToken);
        var selected = graph.Count > 0 ? graph[0] : null;
        var detailState = CommitDetailsState.Empty;
        if (selected is not null && DetailLayout.PaneHeight(height) > 0)
        {
            var details = await _services.CommitDetails.ReadAsync(location, selected.Id, cancellationToken: cancellationToken);
            detailState = details is null
                ? new CommitDetailsState(selected.Id, CommitDetailsStatus.Failed, null, "Commit not found")
                : new CommitDetailsState(selected.Id, CommitDetailsStatus.Loaded, details, string.Empty);
        }

        // The Changes tab shows the diff of its first file beside the file list whenever the screen has room for both.
        arrangement ??= PaneArrangement.Normal;
        if (PaneLayout.ShowsDiffWithChanges(width, height, tab))
        {
            arrangement = arrangement with { IsDiffOpen = true };
        }

        var diff = arrangement.IsDiffOpen && arrangement.Zoomed is null or PaneKind.Diff && selected is not null
            ? await ReadDiffAsync(location, selected, detailState, cancellationToken)
            : null;
        var treeView = tab == BottomTab.Files && selected is not null
            ? await ReadTreeAsync(location, selected, glyphs, cancellationToken)
            : null;
        var content = DetailContentBuilder.Build(selected, detailState, tab, treeView, FilePathStyle.NameFirst);
        var detail = new DetailPaneModel(content, content.Tab == BottomTab.Changes && content.Files.Count > 0 ? content.FirstFileRow : 0, 0, false);
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
        var repositoryState = await ReadTabAsync(location, head, cancellationToken);
        var sidebar = new SidebarModel(SidebarBuilder.Build(repositoryState), 0, 0, string.Empty, false, false);
        var lines = ScreenComposer.Compose(
            model,
            graph,
            ScreenLayout.ReservedLeadingRows,
            0,
            width,
            height,
            glyphs,
            detail,
            sidebar,
            null,
            diff,
            diff is null ? null : arrangement,
            UpstreamGoneBranches.Of(repositoryState.Refs));
        if (colors is not null)
        {
            await output.WriteAsync(AnsiFrameWriter.Render(lines, colors).AsMemory(), cancellationToken);
            return;
        }

        foreach (var line in lines)
        {
            await output.WriteLineAsync(line.Text.AsMemory(), cancellationToken);
        }
    }

    // The tree as a fresh tab shows it: every directory closed.
    private async Task<FileTreeView> ReadTreeAsync(
        RepositoryLocation location,
        GraphEntry selected,
        GraphGlyphSet glyphs,
        CancellationToken cancellationToken)
    {
        var entries = await _services.CommitTree.ReadAsync(location, selected.Id, cancellationToken: cancellationToken);
        var model = FileTreeModel.Build(entries);
        var state = new CommitTreeState(selected.Id, CommitDetailsStatus.Loaded, model, string.Empty);

        return new FileTreeView(
            state,
            model.Rows(new HashSet<string>(), string.Empty),
            string.Empty,
            SidebarGlyphs.For(glyphs));
    }

    private async Task<DiffPaneModel> ReadDiffAsync(RepositoryLocation location, GraphEntry selected, CommitDetailsState detailState, CancellationToken cancellationToken)
    {
        // The diff needs the parents; the detail pane is not read when the terminal is too short for it.
        var details = detailState.Details ?? await _services.CommitDetails.ReadAsync(location, selected.Id, cancellationToken: cancellationToken);
        var firstParent = details is { Parents.Count: > 0 } ? details.Parents[0] : (ObjectId?)null;
        var builder = new DiffDocumentBuilder();
        var warnings = new List<string>();
        var files = _services.Diff.ReadCommitAsync(
            location,
            selected.Id,
            firstParent,
            warnings,
            DiffStore.DefaultFileLineLimit,
            cancellationToken: cancellationToken);
        await foreach (var file in files)
        {
            builder.Add(file);
        }

        return new DiffPaneModel(builder.Build(warnings), 0, 0, false);
    }

    private async Task<RepositoryState> ReadTabAsync(
        RepositoryLocation location,
        HeadState head,
        CancellationToken cancellationToken)
    {
        var refs = await _services.Refs.ReadRefsAsync(location, cancellationToken);
        var worktrees = await _services.Refs.ReadWorktreesAsync(location, cancellationToken);
        var stashes = await _services.Stashes.ReadStashesAsync(location, cancellationToken);
        var tab = RepositoryState.Create(new TabId(1), location);
        foreach (var command in new RepositoryCommand[]
        {
            new HeadLoadedCommand(head),
            new RefsLoadedCommand([.. refs.Items]),
            new WorktreesLoadedCommand([.. worktrees.Items]),
            new StashesLoadedCommand([.. stashes.Items]),
        })
        {
            tab = RepositoryReducer.Reduce(tab, command);
        }

        return tab;
    }
}
