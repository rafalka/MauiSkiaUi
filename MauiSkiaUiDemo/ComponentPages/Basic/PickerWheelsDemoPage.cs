using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiPickerWheels"/>: the multi-column picker's wheels inline, always open, with no field to tap. Two sets of columns
/// from a recurring-schedule editor: "every [n] [unit]" and "distribute [n] [unit] before [start / due date]". The readout below
/// follows the columns, which the wheels write once they have settled (<see cref="SkUiPickerWheels.CommitDelay"/>), not for each
/// row passed. Keys once focused: Left / Right change the column, Up / Down, Page Up / Down, Home / End and typing move the wheel.
/// </summary>
public sealed class PickerWheelsDemoPage : ComponentDemoPage
{
    private const string Every = "Every [n] [unit]";
    private const string Distribute = "Distribute [n] [unit] before [date]";

    private static readonly string[] Counts = [.. Enumerable.Range(1, 99).Select(count => count.ToString(CultureInfo.InvariantCulture))];
    private static readonly string[] Offsets = [.. Enumerable.Range(0, 100).Select(count => count.ToString(CultureInfo.InvariantCulture))];
    private static readonly string[] Units = ["days", "weeks", "months", "years"];
    private static readonly string[] Anchors = ["start date", "due date"];

    private readonly SkUiPickerWheels _wheels;
    private int _commits;
    private bool _loop;

    public PickerWheelsDemoPage()
        : base(nameof(SkUiPickerWheels), new SkUiPickerWheels(), widthRange: (120, 360, 300), heightRange: (80, 360, 200))
    {
        _wheels = (SkUiPickerWheels)SkiaControl;
        _wheels.SelectionChanged += (_, _) => { _commits++; Status(); };

        Choice("Columns", [Every, Distribute], Every, SetColumns, () => _wheels.Columns.Count == 3 ? Distribute : Every);
        Number(nameof(SkUiPickerWheels.VisibleRows), 1, 9, 5, value => _wheels.VisibleRows = (int)value, () => _wheels.VisibleRows, whole: true);
        Number(nameof(SkUiPickerWheels.RowHeight), -1, 64, -1, value => _wheels.RowHeight = value <= 0 ? -1 : value, () => _wheels.RowHeight, whole: true);
        Number("CommitDelay (ms)", 0, 1000, 100, value => _wheels.CommitDelay = TimeSpan.FromMilliseconds(value),
            () => _wheels.CommitDelay.TotalMilliseconds, whole: true);
        Toggle("Number column loops", false, value => { _loop = value; ApplyLoop(); }, () => _loop);
        ColorEditor(nameof(SkUiPickerWheels.TextColor), DemoColors.Ink, value => _wheels.TextColor = value, () => _wheels.TextColor ?? DemoColors.Ink);
        Number(nameof(SkUiPickerWheels.FontSize), 10, 30, 16, value => _wheels.FontSize = value, () => _wheels.FontSize);
        ActionButton("Set from the app: 2 weeks", () =>
        {
            _wheels.Columns[0].SelectedIndex = Array.IndexOf(_wheels.Columns.Count == 3 ? Offsets : Counts, "2");
            _wheels.Columns[1].SelectedIndex = 1;
        });
        SetColumns(Every);
    }

    private void SetColumns(string name)
    {
        _wheels.Columns.Clear();
        if (name == Distribute)
        {
            _wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Offsets, SelectedIndex = 2 }.SetSemanticDescription("Amount"));
            _wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Units, SelectedIndex = 1 }.SetSemanticDescription("Unit"));
            _wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Anchors, SelectedIndex = 1 }.SetSemanticDescription("Before"));
        }
        else
        {
            _wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Counts, SelectedIndex = 0 }.SetSemanticDescription("Amount"));
            _wheels.Columns.Add(new SkUiPickerColumn { ItemsSource = Units, SelectedIndex = 1 }.SetSemanticDescription("Unit"));
        }
        ApplyLoop();
        Status();
    }

    private void ApplyLoop()
    {
        if (_wheels.Columns.Count > 0)
            _wheels.Columns[0].Loop = _loop;
    }

    /// <summary>The readout the wheels sit under, as in the schedule editor, and how many writes the wheels made.</summary>
    private void Status()
    {
        var items = _wheels.SelectedItems;
        var readout = _wheels.Columns.Count == 3
            ? $"Distribute {items[0]} {items[1]} before {items[2]}"
            : $"Every {items.ElementAtOrDefault(0)} {items.ElementAtOrDefault(1)}";
        Feedback($"{readout} · changes {_commits}");
    }
}
