using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Trees;
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
            detailState = new CommitDetailsState(selected.Id, CommitDetailsStatus.Loaded, details, string.Empty);
            if (details is null)
            {
                detailState = new CommitDetailsState(selected.Id, CommitDetailsStatus.Failed, null, "Commit not found");
            }
        }

        // The Changes tab shows the diff of its first file beside the file list whenever the screen has room for both.
        // The Files tab shows the content pane beside the tree from the start, as the live UI does (#784).
        arrangement ??= PaneArrangement.Normal;
        var zoomsPair = arrangement.Zoomed is { } zoomed && PaneZoom.IsZoomPair(zoomed, PaneKind.Diff);
        if (PaneLayout.ShowsDiffWithChanges(width, height, tab))
        {
            // A zoomed detail or diff pane zooms with its partner, as in the live UI (#632, #642, #712).
            arrangement = arrangement with { IsDiffOpen = true, ZoomBoth = zoomsPair };
        }

        var diffVisible = arrangement.Zoomed is null or PaneKind.Diff || arrangement.ZoomBoth;
        var treeView = default(FileTreeView);
        if (tab == BottomTab.Files && selected is not null)
        {
            treeView = await ReadTreeAsync(location, selected, glyphs, cancellationToken);
        }

        var paneVisible = arrangement.IsDiffOpen && diffVisible && selected is not null;
        var diff = default(DiffPaneModel);
        if (paneVisible && tab == BottomTab.Files)
        {
            diff = await ReadFileContentAsync(location, treeView, cancellationToken);
        }
        else if (paneVisible)
        {
            diff = await ReadDiffAsync(location, selected!, detailState, cancellationToken);
        }

        var content = DetailContentBuilder.Build(selected, detailState, tab, treeView, FilePathStyle.NameFirst);
        var firstRow = content.Tab == BottomTab.Changes && content.Files.Count > 0 ? content.FirstFileRow : 0;
        var detail = new DetailPaneModel(content, firstRow, 0, false);
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

    // The content pane of the Files tab for the row under the tree's initial cursor, as the live UI shows it.
    private async Task<DiffPaneModel> ReadFileContentAsync(
        RepositoryLocation location,
        FileTreeView? treeView,
        CancellationToken cancellationToken)
    {
        var row = treeView?.Rows.Count > 0 ? treeView.Rows[0] : null;
        if (row is not { Kind: not FileTreeRowKind.Directory and not FileTreeRowKind.Submodule, Blob: { } blob })
        {
            var text = row?.Kind == FileTreeRowKind.Submodule ? FileContentDocument.SubmodulePlaceholder : FileContentDocument.FilePlaceholder;

            return new DiffPaneModel(FileContentDocument.Placeholder(text), 0, 0, false);
        }

        var content = await _services.Blob.ReadAsync(location, blob, row.Size, cancellationToken);

        return new DiffPaneModel(FileContentDocument.From(row.Path, content), 0, 0, false);
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
