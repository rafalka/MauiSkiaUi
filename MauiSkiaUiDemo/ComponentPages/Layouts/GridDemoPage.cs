using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiGrid"/>.</summary>
public sealed class GridDemoPage : ComponentDemoPage
{
    public GridDemoPage() : base(nameof(SkUiGrid), new SkUiGrid(), new Grid())
    {
        var skia = (SkUiGrid)SkiaControl;
        var native = (Grid)NativeControl!;
        skia.RowDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        native.RowDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        skia.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        native.ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)];
        skia.BackgroundColor = Colors.LightGray;
        native.BackgroundColor = Colors.LightGray;
        for (var index = 0; index < 3; index++)
        {
            var color = index == 0 ? Accent : index == 1 ? DemoColors.SampleA : DemoColors.SampleB;
            var drawn = new SkUiLabel { Text = $"Cell {index + 1}", Background = color, TextColor = Colors.White, FontSize = 14 };
            var standard = new Label { Text = drawn.Text, Background = color, TextColor = Colors.White, FontSize = 14 };
            Grid.SetRow(drawn, index / 2);
            Grid.SetColumn(drawn, index % 2);
            Grid.SetRow(standard, index / 2);
            Grid.SetColumn(standard, index % 2);
            skia.Children.Add(drawn);
            native.Children.Add(standard);
        }
        Number(nameof(SkUiGrid.Padding), 0, 24, 8, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        Number(nameof(SkUiGrid.RowSpacing), 0, 24, 6, value => { skia.RowSpacing = value; native.RowSpacing = value; }, () => skia.RowSpacing, () => native.RowSpacing);
        Number(nameof(SkUiGrid.ColumnSpacing), 0, 24, 6, value => { skia.ColumnSpacing = value; native.ColumnSpacing = value; }, () => skia.ColumnSpacing, () => native.ColumnSpacing);
        Choice("FirstColumn", new[] { GridLength.Auto, GridLength.Star, new GridLength(64) }, GridLength.Star,
            value => { skia.ColumnDefinitions[0].Width = value; native.ColumnDefinitions[0].Width = value; }, () => skia.ColumnDefinitions[0].Width, () => native.ColumnDefinitions[0].Width);
        Toggle("SpanLastCell", true, value => { Grid.SetColumnSpan((BindableObject)skia.Children[2], value ? 2 : 1); Grid.SetColumnSpan((BindableObject)native.Children[2], value ? 2 : 1); },
            () => Grid.GetColumnSpan((BindableObject)skia.Children[2]) == 2, () => Grid.GetColumnSpan((BindableObject)native.Children[2]) == 2);
        // Distinct from the root IsVisible editor on ComponentDemoPage.
        Toggle("FirstCellVisible", true, value => { ((View)skia.Children[0]).IsVisible = value; ((View)native.Children[0]).IsVisible = value; },
            () => ((View)skia.Children[0]).IsVisible, () => ((View)native.Children[0]).IsVisible);
    }
}
