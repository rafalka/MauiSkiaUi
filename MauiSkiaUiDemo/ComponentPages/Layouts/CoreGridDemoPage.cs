using MauiSkiaUi;
using MauiSkiaUi.Core;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>Core grid playground — layout only (labels carry fills; use <see cref="SkUiCoreTable"/> for cell chrome).</summary>
public sealed class CoreGridDemoPage : ComponentDemoPage
{
    private readonly SkUiCoreGrid _grid;
    private readonly SkUiCoreLabel _cell0;
    private readonly SkUiCoreLabel _cell2;
    private SkUiCoreGridLength _firstColumn = SkUiCoreGridLength.Star;

    public CoreGridDemoPage()
        : base(nameof(SkUiCoreGrid), CreateHost(out var grid, out var cell0, out var cell2), native: null,
            widthRange: (120, 360, 280), heightRange: (120, 280, 180))
    {
        _grid = grid;
        _cell0 = cell0;
        _cell2 = cell2;

        Number(nameof(SkUiCoreGrid.Padding), 0, 24, 8, value =>
        {
            _grid.Padding = new Thickness(value);
        }, () => _grid.Padding.Left);

        Number(nameof(SkUiCoreGrid.RowSpacing), 0, 24, 6, value =>
        {
            _grid.RowSpacing = value;
        }, () => _grid.RowSpacing);

        Number(nameof(SkUiCoreGrid.ColumnSpacing), 0, 24, 6, value =>
        {
            _grid.ColumnSpacing = value;
        }, () => _grid.ColumnSpacing);

        Choice("FirstColumn",
            new[] { SkUiCoreGridLength.Auto, SkUiCoreGridLength.Star, new SkUiCoreGridLength(64) },
            SkUiCoreGridLength.Star,
            value =>
            {
                _firstColumn = value;
                _grid.ColumnDefinitions[0].Width = value;
            },
            () => _firstColumn);

        Number("Col0.MinWidth", 0, 120, 0, value =>
        {
            _grid.ColumnDefinitions[0].MinWidth = value;
        }, () => _grid.ColumnDefinitions[0].MinWidth);

        Number("Col0.MaxWidth", 40, 400, 400, value =>
        {
            _grid.ColumnDefinitions[0].MaxWidth = value;
        }, () =>
        {
            var max = _grid.ColumnDefinitions[0].MaxWidth;
            return double.IsPositiveInfinity(max) ? 400 : max;
        });

        Number("Row0.MinHeight", 0, 120, 0, value =>
        {
            _grid.RowDefinitions[0].MinHeight = value;
        }, () => _grid.RowDefinitions[0].MinHeight);

        Number("Row0.MaxHeight", 40, 400, 400, value =>
        {
            _grid.RowDefinitions[0].MaxHeight = value;
        }, () =>
        {
            var max = _grid.RowDefinitions[0].MaxHeight;
            return double.IsPositiveInfinity(max) ? 400 : max;
        });

        Toggle("SpanLastCell", true, value =>
        {
            _grid.SetColumnSpan(_cell2, value ? 2 : 1);
        }, () => _grid.GetPlacement(_cell2).ColumnSpan == 2);

        Toggle("FirstCellVisible", true, value =>
        {
            _cell0.IsVisible = value;
        }, () => _cell0.IsVisible);
    }

    private static SkUiCoreHost CreateHost(
        out SkUiCoreGrid grid,
        out SkUiCoreLabel cell0,
        out SkUiCoreLabel cell2)
    {
        grid = new SkUiCoreGrid()
            .SetPadding(new Thickness(8))
            .SetRowSpacing(6)
            .SetColumnSpacing(6)
            .SetRowDefinitions([
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreRowDefinition(SkUiCoreGridLength.Star)
            ])
            .SetColumnDefinitions([
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star),
                new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star)
            ]);

        cell0 = Cell("Cell 1", Color.FromArgb("#BFDBFE"));
        var cell1 = Cell("Cell 2", Color.FromArgb("#BBF7D0"));
        cell2 = Cell("Cell 3 (span)", Color.FromArgb("#FDE68A"));

        grid.Add(cell0, 0, 0);
        grid.Add(cell1, 0, 1);
        grid.Add(cell2, 1, 0, columnSpan: 2);

        var host = new SkUiCoreHost { BackgroundColor = Colors.LightGray };
        host.SetContent(grid);
        return host;
    }

    private static SkUiCoreLabel Cell(string text, Color background)
    {
        var fill = new SKColor(
            (byte)(background.Red * 255),
            (byte)(background.Green * 255),
            (byte)(background.Blue * 255),
            (byte)(background.Alpha * 255));

        var label = new SkUiCoreLabel()
            .SetText(text)
            .SetTextColor(DemoColors.Ink)
            .SetFontSize(14)
            .SetPadding(new Thickness(8))
            .SetHorizontalTextAlignment(TextAlignment.Center)
            .SetVerticalTextAlignment(TextAlignment.Center);

        label.HorizontalAlignment = LayoutAlignment.Fill;
        label.VerticalAlignment = LayoutAlignment.Fill;
        label.SetPaintBackground(canvas =>
        {
            using var paint = new SKPaint { Color = fill, IsAntialias = true, Style = SKPaintStyle.Fill };
            canvas.DrawRect(0, 0, (float)label.Frame.Width, (float)label.Frame.Height, paint);
        });
        return label;
    }
}
