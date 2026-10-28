using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiGridSplitter"/>: a column splitter between two panes, and a row splitter between the
/// two halves of the right pane. Drag them, or focus one (Tab) and use the arrow keys. Thin splitters take 1 DIP and reach
/// over the panes with their touch area and grip.
/// </summary>
public sealed class GridSplitterDemoPage : ComponentDemoPage
{
    private const string Star = "Star";
    private const string Absolute = "Absolute 120";
    private const string Auto = "Auto";
    private const string LeftColumn = "Left column";
    private const string LeftMinimum = "Left column MinimumLength";
    private const string TopMaximum = "Top row MaximumLength";
    private const string SplitterBackground = "Splitter background";
    private const string ThinSplitters = "Thin splitters (1 DIP)";
    // The top row's MaximumLength editor at its end: no limit.
    private const double NoMaximum = 400;

    public GridSplitterDemoPage()
        : base(nameof(SkUiGridSplitter), CreatePreview(out var columns, out var rows, out var columnSplitter, out var rowSplitter),
            widthRange: (220, 480, 340), heightRange: (160, 480, 260))
    {
        DocumentTypes(typeof(SkUiGrid));
        SkUiGridSplitter[] splitters = [columnSplitter, rowSplitter];

        ColorEditor(nameof(SkUiGridSplitter.GripColor), columnSplitter.GripColor,
            value => { foreach (var splitter in splitters) splitter.GripColor = value; }, () => columnSplitter.GripColor);
        Number(nameof(SkUiGridSplitter.KeyboardIncrement), 1, 50, SkUiDragger.DefaultKeyboardIncrement,
            value => { foreach (var splitter in splitters) splitter.KeyboardIncrement = value; }, () => columnSplitter.KeyboardIncrement, whole: true);
        Toggle(ThinSplitters, false, value =>
        {
            columnSplitter.WidthRequest = value ? 1 : -1;
            rowSplitter.HeightRequest = value ? 1 : -1;
        }, () => columnSplitter.WidthRequest > 0);
        Number(nameof(SkUiGridSplitter.TouchThickness), -1, 48, -1,
            value => { foreach (var splitter in splitters) splitter.TouchThickness = value; }, () => columnSplitter.TouchThickness, whole: true);
        Toggle(nameof(SkUiGridSplitter.ShowsLine), false,
            value => { foreach (var splitter in splitters) splitter.ShowsLine = value; }, () => columnSplitter.ShowsLine);
        Choice(nameof(SkUiGridSplitter.GripAlignment), [LayoutAlignment.Start, LayoutAlignment.Center, LayoutAlignment.End, LayoutAlignment.Fill],
            LayoutAlignment.Center, value => { foreach (var splitter in splitters) splitter.GripAlignment = value; }, () => columnSplitter.GripAlignment);
        Toggle(SplitterBackground, false,
            value => { foreach (var splitter in splitters) splitter.Background = value ? DemoColors.PageBackground : null; },
            () => !Brush.IsNullOrEmpty(columnSplitter.Background));
        Choice(LeftColumn, [Star, Absolute, Auto], Star, value => columns.ColumnDefinitions[0].Width = value switch
        {
            Absolute => new GridLength(120),
            Auto => GridLength.Auto,
            _ => GridLength.Star
        }, () => columns.ColumnDefinitions[0].Width switch
        {
            { IsAbsolute: true } => Absolute,
            { IsAuto: true } => Auto,
            _ => Star
        });
        Number(LeftMinimum, 0, 200, 0, value => SkUiGridSplitter.SetMinimumLength(columns.ColumnDefinitions[0], value),
            () => SkUiGridSplitter.GetMinimumLength(columns.ColumnDefinitions[0]), whole: true);
        Number(TopMaximum, 40, NoMaximum, NoMaximum,
            value => SkUiGridSplitter.SetMaximumLength(rows.RowDefinitions[0], value >= NoMaximum ? double.PositiveInfinity : value),
            () => Math.Min(NoMaximum, SkUiGridSplitter.GetMaximumLength(rows.RowDefinitions[0])), whole: true);
        // A drag turns the tracks it moves into star weights or absolute sizes: Reset makes them equal stars again.
        OnReset(() =>
        {
            columns.ColumnDefinitions[2].Width = GridLength.Star;
            rows.RowDefinitions[0].Height = GridLength.Star;
            rows.RowDefinitions[2].Height = GridLength.Star;
        });

        columnSplitter.DragCompleted += (_, _) => Report(columns.ColumnDefinitions[0].Width, columns.ColumnDefinitions[2].Width, "Columns");
        rowSplitter.DragCompleted += (_, _) => Report(rows.RowDefinitions[0].Height, rows.RowDefinitions[2].Height, "Rows");
    }

    private void Report(GridLength first, GridLength second, string what) => Feedback($"{what}: {Format(first)} | {Format(second)}");

    private static string Format(GridLength length) =>
        length.IsStar ? $"{length.Value:0.##}*" : length.IsAuto ? Auto : $"{length.Value:0}";

    private static SkUiGrid CreatePreview(out SkUiGrid columns, out SkUiGrid rows, out SkUiGridSplitter columnSplitter, out SkUiGridSplitter rowSplitter)
    {
        columnSplitter = new SkUiGridSplitter { AutomationId = "ColumnSplitter" };
        rowSplitter = new SkUiGridSplitter { AutomationId = "RowSplitter" };
        rows = new SkUiGrid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star)] };
        // Each splitter after the panes it sits between: a thin one draws its grip, and takes pointers, over them.
        rows.Add(Pane("Top", DemoColors.SampleA));
        rows.Add(Pane("Bottom", DemoColors.SampleB), 0, 2);
        rows.Add(rowSplitter, 0, 1);
        columns = new SkUiGrid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star)] };
        columns.Add(Pane("Left", Accent));
        columns.Add(rows, 2);
        columns.Add(columnSplitter, 1);
        return columns;
    }

    private static SkUiLabel Pane(string text, Color color) => new()
    {
        Text = text, TextColor = Colors.White, FontSize = 14, Background = color,
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
    };
}
