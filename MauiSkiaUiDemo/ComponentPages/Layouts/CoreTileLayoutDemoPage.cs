using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>Core tile layout playground: a photo gallery of equal tiles with an overflow tile.</summary>
public sealed class CoreTileLayoutDemoPage : ComponentDemoPage
{
    private const int InitialPhotos = 9;

    private static readonly Color[][] Gradients =
    [
        [Color.FromArgb("#0EA5E9"), Color.FromArgb("#6366F1")], [Color.FromArgb("#F59E0B"), Color.FromArgb("#EF4444")],
        [Color.FromArgb("#22C55E"), Color.FromArgb("#0F766E")], [Color.FromArgb("#EC4899"), Color.FromArgb("#8B5CF6")],
    ];

    private int _taps;

    public CoreTileLayoutDemoPage()
        : base(nameof(SkUiCoreTileLayout), CreateHost(out var tiles), native: null, widthRange: (80, 400, 300), heightRange: (60, 360, 220))
    {
        tiles.OverflowTapped += (_, _) => Feedback($"Overflow tapped {++_taps}× · {TileLayoutDemoPage.MorePhotos(tiles.Overflow.HiddenCount)}");
        Number(nameof(SkUiCoreTileLayout.TileSize), 32, 200, 96, value => tiles.TileSize = value, () => tiles.TileSize, whole: true);
        Number(nameof(SkUiCoreTileLayout.MinimumTileSize), 0, 160, 0, value => tiles.MinimumTileSize = value, () => tiles.MinimumTileSize, whole: true);
        Number(nameof(SkUiCoreTileLayout.TileAspectRatio), 0.5, 2, 1, value => tiles.TileAspectRatio = value, () => tiles.TileAspectRatio);
        Number(nameof(SkUiCoreTileLayout.ColumnSpacing), 0, 16, 4, value => tiles.ColumnSpacing = value, () => tiles.ColumnSpacing);
        Number(nameof(SkUiCoreTileLayout.RowSpacing), 0, 16, 4, value => tiles.RowSpacing = value, () => tiles.RowSpacing);
        Choice("MaximumRows (-1: no limit)", TileLayoutDemoPage.RowLimits, 2, value => tiles.MaximumRows = value, () => tiles.MaximumRows);
        Toggle("OverflowTile", true, value => tiles.OverflowTemplate = value ? Overflow : null, () => tiles.OverflowTemplate is not null);
        ActionButton("Add photo", () => AddPhotos(tiles, 1));
        ActionButton("Remove photo", () => { if (tiles.Children.Count > 1) tiles.Remove(tiles.Children[^1]); });
        OnReset(() =>
        {
            while (tiles.Children.Count > InitialPhotos) tiles.Remove(tiles.Children[^1]);
            AddPhotos(tiles, InitialPhotos - tiles.Children.Count);
            _taps = 0;
        });
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreTileLayout tiles)
    {
        tiles = new SkUiCoreTileLayout().SetTileSize(96).SetColumnSpacing(4).SetRowSpacing(4).SetMaximumRows(2).SetOverflowTemplate(Overflow);
        AddPhotos(tiles, InitialPhotos);
        var host = new SkUiCoreHost { BackgroundColor = Colors.LightGray };
        host.SetContent(tiles);
        return host;
    }

    private static void AddPhotos(SkUiCoreTileLayout tiles, int count)
    {
        for (var index = 0; index < count; index++)
            tiles.Add(Photo(tiles.Children.Count));
    }

    private static SkUiCoreBorder Photo(int index)
    {
        var colors = Gradients[index % Gradients.Length];
        // In a corner, so the overflow tile's count over the last photo stays readable.
        var number = new SkUiCoreLabel().SetText((index + 1).ToString()).SetTextColor(Colors.White).SetFontSize(13)
            .SetFontAttributes(FontAttributes.Bold);
        number.SetHorizontalAlignment(LayoutAlignment.Start).SetVerticalAlignment(LayoutAlignment.Start).SetMargin(new Thickness(6, 4));
        var photo = new SkUiCoreBorder().SetStrokeThickness(0).SetCornerRadius(6).SetContent(number);
        photo.SetBackground(new LinearGradientPaint([new PaintGradientStop(0, colors[0]), new PaintGradientStop(1, colors[1])], new Point(0, 0), new Point(1, 1)));
        return photo;
    }

    /// <summary>"+5" over the last photo, dimming it; follows the count.</summary>
    private static SkUiCoreNode Overflow(SkUiTileOverflow overflow)
    {
        var count = new OverflowCount(overflow).SetTextColor(Colors.White).SetFontSize(22).SetFontAttributes(FontAttributes.Bold);
        count.SetHorizontalAlignment(LayoutAlignment.Center).SetVerticalAlignment(LayoutAlignment.Center);
        var dim = new SkUiCoreBorder().SetStrokeThickness(0).SetCornerRadius(6).SetContent(count);
        dim.SetBackground(Color.FromRgba(0, 0, 0, 0.55));
        return dim;
    }

    /// <summary>
    /// "+N", following <see cref="SkUiTileOverflow.HiddenCount"/> weakly: the overflow lives as long as the layout, and a template
    /// runs again whenever it is set, so a plain subscription would keep every replaced label alive.
    /// </summary>
    private sealed class OverflowCount : SkUiCoreLabel
    {
        // Held by the label: the overflow holds its listeners weakly.
        private readonly SkUiWeakListener<OverflowCount> _listener;
        private readonly SkUiTileOverflow _overflow;

        public OverflowCount(SkUiTileOverflow overflow)
        {
            _overflow = overflow;
            _listener = new SkUiWeakListener<OverflowCount>(this, static (label, _) => label.Update());
            _listener.Listen(overflow);
            Update();
        }

        private void Update() => SetText($"+{_overflow.HiddenCount}");
    }
}
