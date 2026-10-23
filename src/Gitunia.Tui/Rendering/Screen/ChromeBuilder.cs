using System.Globalization;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.Tabs;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>Builds the tab row and status bar lines.</summary>
public static class ChromeBuilder
{
    private const string HintGap = "  ";

    public static StyledLine Tabs(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        if (model.Tabs is { Count: > 0 } labels)
        {
            return TabRow(labels, width, glyphs, model.RemoteBusy, model.Keys);
        }

        if (model.TabCount == 0)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Append(" no repository open", SegmentRole.Status).Build(SegmentRole.Status);
        }

        return new LineBuilder(width, glyphs.Ellipsis)
            .Append(" ", SegmentRole.Status)
            .Append($"  {model.TabNumber} {model.RepositoryName}  ", SegmentRole.ActiveTab)
            .Append(model.TabCount > 1 ? $"  {model.TabCount} tabs" : string.Empty, SegmentRole.Status)
            .Build(SegmentRole.Status);
    }

    // The selected sidebar name gets at most a third of the line (cut in the middle), so hints and messages stay.
    private const int NameShareDivisor = 3;
    private const int MinimumNameColumns = 12;

    public static StyledLine Status(ScreenModel model, int width, GraphGlyphSet glyphs)
    {
        var selected = model.SelectedPosition.ToString(CultureInfo.InvariantCulture);
        var loaded = model.LoadedCount.ToString(CultureInfo.InvariantCulture);
        var more = model.IsLoading || model.TruncatedTotal is not null ? "+" : string.Empty;
        var position = $"{selected}/{loaded}{more}";
        if (model.TruncatedTotal is { } total)
        {
            position += $" of {total.ToString(CultureInfo.InvariantCulture)}";
        }

        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append($" {model.ModeName} ", SegmentRole.StatusMode)
            .Append(" ", SegmentRole.Status);
        if (model.PendingKeys.Length > 0)
        {
            builder.Append($"{model.PendingKeys} ", SegmentRole.StatusMode);
        }

        builder.Append($"{(model.SearchLine.Length > 0 ? model.SearchLine : position)} ", SegmentRole.Status);
        if (model.SearchLine.Length == 0 && model.SelectedName.Length > 0)
        {
            var name = TextWidth.FitMiddle(model.SelectedName, Math.Max(MinimumNameColumns, width / NameShareDivisor), glyphs.Ellipsis);
            builder.Append($"{name} ", SegmentRole.Status);
        }

        if (model.IsLoading)
        {
            builder.Append("loading... ", SegmentRole.Status);
        }

        if (model.Message.Length > 0)
        {
            builder.Append($" {model.Message} ", MessageRole(model));
        }

        if (model.SearchLine.Length == 0)
        {
            AppendHints(builder, model.Picker is not null ? PaneHints.ForPicker() : model.Hints ?? PaneHints.For(PaneKind.Graph));
        }

        return builder.Build(SegmentRole.Status);
    }

    // One role per severity, a filled chip while the message is fresh. No severity (a key's own notice, progress)
    // keeps the plain bar style, except progress of a running remote operation.
    private static SegmentRole MessageRole(ScreenModel model) => model.MessageSeverity switch
    {
        null => model.RemoteBusy ? SegmentRole.StatusProgress : SegmentRole.Status,
        StatusSeverity.Error => model.MessageIsFresh ? SegmentRole.StatusErrorFresh : SegmentRole.StatusError,
        StatusSeverity.Warning => model.MessageIsFresh ? SegmentRole.StatusWarningFresh : SegmentRole.StatusWarning,
        _ => model.MessageIsFresh ? SegmentRole.StatusNoticeFresh : SegmentRole.StatusNotice,
    };

    private static void AppendHints(LineBuilder builder, IReadOnlyList<KeyHint> hints)
    {
        HintRenderer.Append(builder, hints, HintGap);
    }

    private static StyledLine TabRow(
        IReadOnlyList<TabLabel> labels,
        int width,
        GraphGlyphSet glyphs,
        bool remoteBusy,
        ChipKeys? keys)
    {
        var cells = TabBarLayout.Layout(labels, width, glyphs, keys);
        var builder = new LineBuilder(width, glyphs.Ellipsis).Append(new string(' ', TabBarLayout.LeftMargin), SegmentRole.Status);
        if (cells.Count > 0 && cells[0].Number > 1)
        {
            builder.Append("< ", SegmentRole.Status);
        }

        for (var index = 0; index < cells.Count; index++)
        {
            builder.Append(index > 0 ? " " : string.Empty, SegmentRole.Status);
            builder.Append(
                cells[index].IsActive ? cells[index].Text : ButtonLabel.For(cells[index].Text, glyphs.IsAscii),
                cells[index].IsActive ? SegmentRole.ActiveTab : SegmentRole.RepositoryTab);
        }

        builder.Append(" ", SegmentRole.Status);
        builder.Append(TabBarLayout.NewTabLabel, SegmentRole.BarButton);
        if (RemoteChips.Start(width, keys) is { } chipsStart)
        {
            AppendRemoteChips(builder, width, chipsStart, remoteBusy, glyphs, keys);
        }

        if (TabBarLayout.CommandsStart(width) is { } commandsStart)
        {
            builder.Append(new string(' ', Math.Max(0, commandsStart - (width - builder.Remaining))), SegmentRole.Status);
            builder.Append(TabBarLayout.CommandsLabel, SegmentRole.BarButton);
        }

        return builder.Build(SegmentRole.Status);
    }

    private static void AppendRemoteChips(
        LineBuilder builder,
        int width,
        int start,
        bool isBusy,
        GraphGlyphSet glyphs,
        ChipKeys? keys)
    {
        builder.Append(new string(' ', Math.Max(0, start - (width - builder.Remaining))), SegmentRole.Status);
        var first = true;
        foreach (var chip in RemoteChips.Shown(width, isBusy, keys))
        {
            builder.Append(first ? string.Empty : " ", SegmentRole.Status);

            // The key is styled like a status bar hint: bold accent key, plain description.
            var pieces = ChipSegments.Split(
                ButtonLabel.For(chip.Label, glyphs.IsAscii),
                chip.KeyLength,
                SegmentRole.BarButton,
                glyphs.IsAscii ? SegmentRole.BarButton : SegmentRole.Status,
                SegmentRole.StatusKey,
                SegmentRole.Status);
            foreach (var piece in pieces)
            {
                builder.Append(piece.Text, piece.Role);
            }

            first = false;
        }
    }
}
