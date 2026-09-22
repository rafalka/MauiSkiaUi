using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Core table playground: row/column fills, separators, header merge, and mid-table merged cells.
/// </summary>
public sealed class CoreTableDemoPage : ComponentDemoPage
{
    private readonly SkUiCoreTable _table;
    private Color _rowSeparator = Colors.Gray;
    private Color _headerRow = Color.FromArgb("#DBEAFE");
    private SkUiCoreTableTrackBackgroundOrder _order = SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows;

    public CoreTableDemoPage()
        : base(nameof(SkUiCoreTable), CreateHost(out var table), native: null,
            widthRange: (200, 480, 360), heightRange: (180, 400, 280))
    {
        _table = table;

        Number(nameof(SkUiCoreTable.RowSeparatorThickness), 0, 6, 1, value =>
        {
            _table.RowSeparatorThickness = value;
        }, () => _table.RowSeparatorThickness);

        Number(nameof(SkUiCoreTable.ColumnSeparatorThickness), 0, 6, 1, value =>
        {
            _table.ColumnSeparatorThickness = value;
        }, () => _table.ColumnSeparatorThickness);

        ColorEditor("RowSeparatorColor", Colors.Gray, value =>
        {
            _rowSeparator = value;
            _table.RowSeparatorColor = value;
            _table.ColumnSeparatorColor = value;
        }, () => _rowSeparator);

        ColorEditor("HeaderRowBackground", Color.FromArgb("#DBEAFE"), value =>
        {
            _headerRow = value;
            _table.SetRowBackground(0, value);
        }, () => _headerRow);

        Choice(nameof(SkUiCoreTable.TrackBackgroundOrder),
            Enum.GetValues<SkUiCoreTableTrackBackgroundOrder>(),
            SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows,
            value =>
            {
                _order = value;
                _table.TrackBackgroundOrder = value;
            },
            () => _order);

        Toggle("ShowColumnTint", true, value =>
        {
            if (value)
                _table.SetColumnBackground(0, Color.FromArgb("#FEF3C7"));
            else
                _table.SetColumnBackground(0, null);
        }, () => _table.GetColumnBackground(0) is not null);
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreTable table)
    {
        // 5 rows × 4 columns with merges: full header, mid-table column span, and a tall row span.
        table = new SkUiCoreTable();
        table.SetPadding(new Thickness(4))
            .SetRowSpacing(0)
            .SetColumnSpacing(0)
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(new SkUiCoreGridLength(36)),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
            ])
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(new SkUiCoreGridLength(72)),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ]);

        table.SetRowBackground(0, Color.FromArgb("#DBEAFE"));
        table.SetRowBackground(1, Color.FromArgb("#F8FAFC"));
        table.SetRowBackground(2, Colors.White);
        table.SetRowBackground(3, Color.FromArgb("#F8FAFC"));
        table.SetRowBackground(4, Colors.White);
        table.SetColumnBackground(0, Color.FromArgb("#FEF3C7"));
        table.SetRowSeparatorColor(Colors.Gray)
            .SetColumnSeparatorColor(Colors.Gray)
            .SetRowSeparatorThickness(1)
            .SetColumnSeparatorThickness(1)
            .SetTrackBackgroundOrder(SkUiCoreTableTrackBackgroundOrder.ColumnsOverRows);

        // Full-cell fills (behind centered labels with margin)
        table.SetCellBackground(0, 0, Color.FromArgb("#93C5FD"), columnSpan: 4);
        table.SetCellBackground(2, 0, Color.FromArgb("#FDE68A"), rowSpan: 2);
        table.SetCellBackground(2, 1, Color.FromArgb("#BBF7D0"), rowSpan: 2, columnSpan: 2);
        table.SetCellBackground(4, 1, Color.FromArgb("#E9D5FF"), columnSpan: 2);

        // Row 0: full-width header
        table.Add(Cell("Quarterly summary", DemoColors.Ink), 0, 0, columnSpan: 4);

        // Row 1: normal cells
        table.Add(Cell("Team", DemoColors.Ink), 1, 0);
        table.Add(Cell("Q1", DemoColors.Ink), 1, 1);
        table.Add(Cell("Q2", DemoColors.Ink), 1, 2);
        table.Add(Cell("Q3", DemoColors.Ink), 1, 3);

        // Row 2–3: mid-table merges — tall label in col0, wide note across Q1–Q2 on row 2,
        // and a 2×2 block in the center (rows 2–3, cols 1–2).
        table.Add(Cell("Alpha (2 rows)", DemoColors.Ink), 2, 0, rowSpan: 2);
        table.Add(Cell("Merged mid block", DemoColors.Ink), 2, 1, rowSpan: 2, columnSpan: 2);
        table.Add(Cell("9", DemoColors.Ink), 2, 3);
        table.Add(Cell("11", DemoColors.Ink), 3, 3);

        // Row 4: column span in the middle columns only
        table.Add(Cell("Beta", DemoColors.Ink), 4, 0);
        table.Add(Cell("Notes spanning Q1–Q2", DemoColors.Ink), 4, 1, columnSpan: 2);
        table.Add(Cell("7", DemoColors.Ink), 4, 3);

        var host = new SkUiCoreHost { BackgroundColor = Colors.White };
        host.SetContent(table);
        return host;
    }

    private static SkUiCoreLabel Cell(string text, Color color)
    {
        var label = new SkUiCoreLabel()
            .SetText(text)
            .SetTextColor(color)
            .SetFontSize(12)
            .SetLineBreakMode(LineBreakMode.WordWrap);
        label.Margin = new Thickness(6);
        label.HorizontalAlignment = LayoutAlignment.Center;
        label.VerticalAlignment = LayoutAlignment.Center;
        return label;
    }
}
