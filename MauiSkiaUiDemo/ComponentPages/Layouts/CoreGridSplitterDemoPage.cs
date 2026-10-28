using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Property playground for <see cref="SkUiCoreGridSplitter"/>: resizable columns in a <see cref="SkUiCoreTable"/>. A splitter
/// at the end of each header cell resizes its column and the next one (or its column alone); one can run down the whole table
/// with its grip in the header. The splitters are 1 DIP wide by default: their touch area and grip reach over the cells.
/// </summary>
public sealed class CoreGridSplitterDemoPage : ComponentDemoPage
{
    private const string FirstMinWidth = "Col0.MinWidth";
    private const string DownTheTable = "Splitters down the whole table";
    private const string ThinSplitters = "Thin splitters (1 DIP)";
    private const string Pair = "Column and next";
    private const string Alone = "Column alone";
    private const double HeaderHeight = 36;
    private const double ColumnWidth = 90;
    private static readonly string[] Headers = ["Name", "Role", "City"];
    private static readonly string[][] Rows =
    [
        ["Ada", "Engineer", "London"],
        ["Grace", "Admiral", "New York"],
        ["Linus", "Maintainer", "Portland"],
        ["Margaret", "Director", "Boston"]
    ];

    public CoreGridSplitterDemoPage()
        : base(nameof(SkUiCoreGridSplitter), CreateHost(out var table, out var splitters), native: null,
            widthRange: (200, 480, 340), heightRange: (120, 400, 200))
    {
        DocumentTypes(typeof(SkUiCoreColumnDefinition), typeof(SkUiCoreTable));
        Choice(nameof(SkUiCoreGridSplitter.ResizeBehavior), [Pair, Alone], Pair, value =>
        {
            foreach (var splitter in splitters)
                splitter.ResizeBehavior = value == Alone ? SkUiGridResizeBehavior.CurrentOnly : SkUiGridResizeBehavior.BasedOnAlignment;
        }, () => splitters[0].ResizeBehavior == SkUiGridResizeBehavior.CurrentOnly ? Alone : Pair);
        Toggle(DownTheTable, false, value =>
        {
            // The splitters' lines replace the table's column separators (a line runs through a splitter's middle, inside its cell).
            table.SetColumnSeparatorColor(value ? null : Colors.LightGray);
            foreach (var splitter in splitters)
            {
                table.SetRowSpan(splitter, value ? Rows.Length + 1 : 1);
                splitter.SetShowsLine(value).SetGripAlignment(value ? LayoutAlignment.Start : LayoutAlignment.Center);
            }
        }, () => table.GetRowSpan(splitters[0]) > 1);
        Toggle(ThinSplitters, true, value =>
        {
            foreach (var splitter in splitters)
                splitter.SetWidth(value ? 1 : double.NaN);
        }, () => splitters[0].Width > 0);
        Number(nameof(SkUiCoreGridSplitter.TouchThickness), -1, 48, -1,
            value => { foreach (var splitter in splitters) splitter.TouchThickness = value; }, () => splitters[0].TouchThickness, whole: true);
        ColorEditor(nameof(SkUiCoreGridSplitter.GripColor), splitters[0].GripColor,
            value => { foreach (var splitter in splitters) splitter.GripColor = value; }, () => splitters[0].GripColor);
        Number(nameof(SkUiCoreGridSplitter.KeyboardIncrement), 1, 50, SkUiDragger.DefaultKeyboardIncrement,
            value => { foreach (var splitter in splitters) splitter.KeyboardIncrement = value; }, () => splitters[0].KeyboardIncrement, whole: true);
        Number(FirstMinWidth, 0, 90, 40, value => table.ColumnDefinitions[0].MinWidth = value, () => table.ColumnDefinitions[0].MinWidth, whole: true);
        OnReset(() =>
        {
            for (var column = 0; column < Headers.Length - 1; column++)
                table.ColumnDefinitions[column].Width = new SkUiCoreGridLength(ColumnWidth);
        });
        foreach (var splitter in splitters)
            splitter.DragCompleted += (_, _) =>
                Feedback($"Columns {table.GetColumnWidth(0):F0} | {table.GetColumnWidth(1):F0} | {table.GetColumnWidth(2):F0}");
    }

    private static SkUiCoreHost CreateHost(out SkUiCoreTable table, out SkUiCoreGridSplitter[] splitters)
    {
        // Fixed columns, the last one taking the rest.
        table = new SkUiCoreTable()
            .SetRowDefinitions([new(new SkUiCoreGridLength(HeaderHeight)), .. Rows.Select(_ => new SkUiCoreRowDefinition(SkUiCoreGridLength.Auto))])
            .SetColumnDefinitions([.. Headers.Select((_, index) => index < Headers.Length - 1
                ? new SkUiCoreColumnDefinition(new SkUiCoreGridLength(ColumnWidth))
                : new SkUiCoreColumnDefinition(SkUiCoreGridLength.Star))])
            .SetRowSeparatorColor(Colors.LightGray)
            .SetColumnSeparatorColor(Colors.LightGray)
            .SetRowBackground(0, Color.FromArgb("#E2E8F0"));
        for (var column = 0; column < Headers.Length; column++)
            table.Add(Cell(Headers[column], bold: true), 0, column);
        for (var row = 0; row < Rows.Length; row++)
            for (var column = 0; column < Headers.Length; column++)
                table.Add(Cell(Rows[row][column], bold: false), row + 1, column);
        // A splitter at the end of every header cell but the last: it resizes its column and the next one. 1 DIP wide, on the
        // column separator, added after the cells so its touch area and grip are on top of them.
        splitters = new SkUiCoreGridSplitter[Headers.Length - 1];
        for (var column = 0; column < splitters.Length; column++)
        {
            var splitter = new SkUiCoreGridSplitter();
            splitter.SetWidth(1).SetHorizontalAlignment(LayoutAlignment.End);
            table.Add(splitter, 0, column);
            splitters[column] = splitter;
        }
        table.ColumnDefinitions[0].MinWidth = 40;
        var host = new SkUiCoreHost { BackgroundColor = Colors.White };
        host.SetContent(table);
        return host;
    }

    private static SkUiCoreLabel Cell(string text, bool bold) => new SkUiCoreLabel()
        .SetText(text)
        .SetTextColor(DemoColors.Ink)
        .SetFontSize(14)
        .SetFontAttributes(bold ? FontAttributes.Bold : FontAttributes.None)
        .SetPadding(new Thickness(8, 6))
        .SetLineBreakMode(LineBreakMode.TailTruncation)
        .SetVerticalTextAlignment(TextAlignment.Center);
}
