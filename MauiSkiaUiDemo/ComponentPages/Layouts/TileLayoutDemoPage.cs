using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Property playground for <see cref="SkUiTileLayout"/>: a photo gallery of equal tiles with an overflow tile.</summary>
public sealed class TileLayoutDemoPage : ComponentDemoPage
{
    private const int InitialPhotos = 9;

    private static readonly Color[][] Gradients =
    [
        [Color.FromArgb("#0EA5E9"), Color.FromArgb("#6366F1")], [Color.FromArgb("#F59E0B"), Color.FromArgb("#EF4444")],
        [Color.FromArgb("#22C55E"), Color.FromArgb("#0F766E")], [Color.FromArgb("#EC4899"), Color.FromArgb("#8B5CF6")],
    ];

    /// <summary>The row limits offered (0 is not a valid limit).</summary>
    internal static readonly int[] RowLimits = [-1, 1, 2, 3, 4];

    private int _taps;

    public TileLayoutDemoPage()
        : base(nameof(SkUiTileLayout), new SkUiTileLayout(), widthRange: (80, 400, 300), heightRange: (60, 360, 220))
    {
        var tiles = (SkUiTileLayout)SkiaControl;
        tiles.BackgroundColor = Colors.LightGray;
        tiles.TileSize = 96;
        tiles.ColumnSpacing = tiles.RowSpacing = 4;
        tiles.MaximumRows = 2;
        tiles.OverflowTemplate = OverflowTemplate();
        tiles.OverflowTapped += (_, _) => Feedback($"Overflow tapped {++_taps}× · {MorePhotos(tiles.Overflow.HiddenCount)}");
        AddPhotos(tiles, InitialPhotos);

        Number(nameof(SkUiTileLayout.TileSize), 32, 200, 96, value => tiles.TileSize = value, () => tiles.TileSize, whole: true);
        Number(nameof(SkUiTileLayout.MinimumTileSize), 0, 160, 0, value => tiles.MinimumTileSize = value, () => tiles.MinimumTileSize, whole: true);
        Number(nameof(SkUiTileLayout.TileAspectRatio), 0.5, 2, 1, value => tiles.TileAspectRatio = value, () => tiles.TileAspectRatio);
        Number(nameof(SkUiTileLayout.ColumnSpacing), 0, 16, 4, value => tiles.ColumnSpacing = value, () => tiles.ColumnSpacing);
        Number(nameof(SkUiTileLayout.RowSpacing), 0, 16, 4, value => tiles.RowSpacing = value, () => tiles.RowSpacing);
        Number(nameof(SkUiTileLayout.Padding), 0, 24, 0, value => tiles.Padding = value, () => tiles.Padding.Left);
        Choice("MaximumRows (-1: no limit)", RowLimits, 2, value => tiles.MaximumRows = value, () => tiles.MaximumRows);
        Toggle("OverflowTile", true, value => tiles.OverflowTemplate = value ? OverflowTemplate() : null, () => tiles.OverflowTemplate is not null);
        Toggle("SecondPhotoVisible", true, value => ((SkUiView)tiles.Children[1]).IsVisible = value, () => ((SkUiView)tiles.Children[1]).IsVisible);
        ActionButton("Add photo", () => AddPhotos(tiles, 1));
        // Down to two photos: the second one is SecondPhotoVisible's.
        ActionButton("Remove photo", () => { if (tiles.Children.Count > 2) tiles.Children.RemoveAt(tiles.Children.Count - 1); });
        OnReset(() =>
        {
            while (tiles.Children.Count > InitialPhotos) tiles.Children.RemoveAt(tiles.Children.Count - 1);
            AddPhotos(tiles, InitialPhotos - tiles.Children.Count);
            ((SkUiView)tiles.Children[1]).IsVisible = true;
            _taps = 0;
        });
    }

    private static void AddPhotos(SkUiTileLayout tiles, int count)
    {
        for (var index = 0; index < count; index++)
            tiles.Children.Add(Photo(tiles.Children.Count));
    }

    private static SkUiBorder Photo(int index)
    {
        var colors = Gradients[index % Gradients.Length];
        return new SkUiBorder
        {
            StrokeThickness = 0, CornerRadius = 6,
            Background = new LinearGradientBrush([new GradientStop(colors[0], 0), new GradientStop(colors[1], 1)], new Point(0, 0), new Point(1, 1)),
            Content = new SkUiLabel
            {
                // In a corner, so the overflow tile's count over the last photo stays readable.
                Text = (index + 1).ToString(), TextColor = Colors.White, FontSize = 13, FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(6, 4)
            }
        };
    }

    internal static string MorePhotos(int count) => count == 1 ? "1 more photo" : $"{count} more photos";

    /// <summary>"+5" over the last photo, dimming it.</summary>
    private static DataTemplate OverflowTemplate() => new(() =>
    {
        var count = new SkUiLabel
        {
            TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        count.SetBinding(SkUiLabel.TextProperty, new Binding(nameof(SkUiTileOverflow.HiddenCount), stringFormat: "+{0}"));
        return new SkUiBorder { StrokeThickness = 0, CornerRadius = 6, Background = Color.FromRgba(0, 0, 0, 0.55), Content = count };
    });
}
