using System.Globalization;
using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiMultiColumnPicker"/>: a field and a popup of wheels, one per <see cref="SkUiPickerColumn"/>, on the app's popup
/// system. Three sets of columns (a time, a date, a unit and an amount) show looping and stopping wheels, items of different
/// widths and a custom format. Scroll, tap or use the keys in the popup (Left / Right change the column, Up / Down, Page Up / Down,
/// Home / End, typing move the wheel, Enter or Escape close); every wheel's change applies at once.
/// </summary>
public sealed class MultiColumnPickerDemoPage : ComponentDemoPage
{
    private const string Time = "Time (hours : minutes AM/PM)";
    private const string Date = "Date (month day year)";
    private const string Unit = "Amount and unit";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string ActionSheetPresenter = "MAUI action sheet";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";
    private const string Joined = "(joined by Separator)";
    private const string TimeFormat = "{0}:{1} {2}";
    private const string PickTime = "Pick a time";
    private const string AppAcceptText = "(app's: SkUiStrings.Current)";
    private const string LocalAcceptText = "Done";

    private static readonly string[] Hours = [.. Enumerable.Range(1, 12).Select(hour => hour.ToString("00", CultureInfo.InvariantCulture))];
    private static readonly string[] Minutes = [.. Enumerable.Range(0, 60).Select(minute => minute.ToString("00", CultureInfo.InvariantCulture))];
    private static readonly string[] DayParts = ["AM", "PM"];
    private static readonly string[] Months = [.. CultureInfo.InvariantCulture.DateTimeFormat.MonthNames.Where(name => name.Length > 0)];
    private static readonly string[] Days = [.. Enumerable.Range(1, 31).Select(day => day.ToString(CultureInfo.InvariantCulture))];
    private static readonly string[] Years = [.. Enumerable.Range(2020, 21).Select(year => year.ToString(CultureInfo.InvariantCulture))];
    private static readonly string[] Amounts = [.. Enumerable.Range(1, 99).Select(amount => amount.ToString(CultureInfo.InvariantCulture))];
    private static readonly string[] Units = ["g", "kg", "ml", "l", "cups"];

    private readonly SkUiMultiColumnPicker _picker;
    private int _opened;
    private int _closed;
    private bool _loop;
    private double _firstWidth = -1;
    private bool _headers;

    public MultiColumnPickerDemoPage()
        : base(nameof(SkUiMultiColumnPicker), new SkUiMultiColumnPicker { Title = PickTime }, widthRange: (100, 360, 240), heightRange: (30, 120, 48))
    {
        _picker = (SkUiMultiColumnPicker)SkiaControl;
        _picker.SelectionChanged += (_, _) => Status();
        _picker.Opened += (_, _) => { _opened++; Status(); };
        _picker.Closed += (_, _) => { _closed++; Status(); };

        Choice("Columns", [Time, Date, Unit], Time, SetColumns, ColumnsName);
        Text(nameof(SkUiMultiColumnPicker.Title), PickTime, value => _picker.Title = value, () => _picker.Title ?? "");
        Text(nameof(SkUiMultiColumnPicker.Separator), SkUiMultiColumnPicker.DefaultSeparator, value => _picker.Separator = value,
            () => _picker.Separator);
        Choice(nameof(SkUiMultiColumnPicker.Format), [Joined, TimeFormat], Joined, value => _picker.Format = value == Joined ? null : value,
            () => _picker.Format ?? Joined);
        Number(nameof(SkUiMultiColumnPicker.VisibleRows), 1, 9, 5, value => _picker.VisibleRows = (int)value, () => _picker.VisibleRows, whole: true);
        Toggle("Columns loop", false, value => { _loop = value; ApplyColumnSettings(); }, () => _loop);
        Number("Column 1 Width", -1, 200, -1, value => { _firstWidth = value < 0 ? -1 : value; ApplyColumnSettings(); }, () => _firstWidth, whole: true);
        ColorEditor(nameof(SkUiMultiColumnPicker.TextColor), DemoColors.Ink, value => _picker.TextColor = value, () => _picker.TextColor ?? DemoColors.Ink);
        ColorEditor(nameof(SkUiMultiColumnPicker.TitleColor), DemoColors.Caption, value => _picker.TitleColor = value, () => _picker.TitleColor ?? DemoColors.Caption);
        Number(nameof(SkUiMultiColumnPicker.FontSize), 10, 30, 16, value => _picker.FontSize = value, () => _picker.FontSize);
        Choice(nameof(SkUiMultiColumnPicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _picker.PopupPlacement = value, () => _picker.PopupPlacement);
        Choice(nameof(SkUiMultiColumnPicker.PopupPresenter), [ToolkitPresenter, ActionSheetPresenter, AppPresenter], ToolkitPresenter,
            value => _picker.PopupPresenter = value switch
            {
                ToolkitPresenter => SkUiToolkitPopupPresenter.Instance,
                ActionSheetPresenter => SkUiActionSheetPopupPresenter.Instance,
                _ => null
            },
            () => _picker.PopupPresenter switch
            {
                SkUiToolkitPopupPresenter => ToolkitPresenter,
                SkUiActionSheetPopupPresenter => ActionSheetPresenter,
                _ => AppPresenter
            });
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _picker.UpdateMode = value, () => _picker.UpdateMode);
        Choice(nameof(SkUiPickerBase.AcceptText), [AppAcceptText, LocalAcceptText], AppAcceptText,
            value => _picker.AcceptText = value == AppAcceptText ? null : value, () => _picker.AcceptText ?? AppAcceptText);
        Toggle("Column headers", false, value => { _headers = value; ApplyColumnSettings(); }, () => _headers);
        Toggle(nameof(SkUiMultiColumnPicker.IsClearButtonVisible), false, value => _picker.IsClearButtonVisible = value, () => _picker.IsClearButtonVisible);
        Toggle(nameof(SkUiMultiColumnPicker.IsPopupClearButtonVisible), false, value => _picker.IsPopupClearButtonVisible = value, () => _picker.IsPopupClearButtonVisible);
        Choice(nameof(SkUiMultiColumnPicker.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _picker.UpdateMode = value, () => _picker.UpdateMode);
        ActionButton("Open (IsOpen = true)", () => _picker.IsOpen = true);
        ActionButton("Choose the first item of every column", () => { foreach (var column in _picker.Columns) column.SelectedIndex = 0; });
        SetColumns(Time);
    }

    private void SetColumns(string name)
    {
        _picker.Columns.Clear();
        switch (name)
        {
            case Date:
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Months });
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Days, Width = 60 });
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Years });
                break;
            case Unit:
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Amounts });
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Units });
                break;
            default:
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Hours });
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = Minutes });
                _picker.Columns.Add(new SkUiPickerColumn { ItemsSource = DayParts });
                break;
        }
        ApplyColumnSettings();
        Status();
    }

    /// <summary>Every column loops (or none), and the first has the width set (a set of columns keeps the settings).</summary>
    private void ApplyColumnSettings()
    {
        foreach (var column in _picker.Columns)
        {
            column.Loop = _loop;
            column.Header = _headers ? HeaderOf(column) : null;
        }
        if (_picker.Columns.Count > 0)
            _picker.Columns[0].Width = _firstWidth;
    }

    /// <summary>A column's header: what its items are.</summary>
    private static string? HeaderOf(SkUiPickerColumn column) => column.ItemsSource switch
    {
        _ when ReferenceEquals(column.ItemsSource, Hours) => "Hour",
        _ when ReferenceEquals(column.ItemsSource, Minutes) => "Minute",
        _ when ReferenceEquals(column.ItemsSource, Months) => "Month",
        _ when ReferenceEquals(column.ItemsSource, Days) => "Day",
        _ when ReferenceEquals(column.ItemsSource, Years) => "Year",
        _ when ReferenceEquals(column.ItemsSource, Amounts) => "Amount",
        _ when ReferenceEquals(column.ItemsSource, Units) => "Unit",
        _ => null
    };

    private string ColumnsName() => _picker.Columns.Count switch { 3 when ReferenceEquals(_picker.Columns[0].ItemsSource, Hours) => Time, 3 => Date, _ => Unit };

    private void Status() => Feedback($"SelectedIndices [{string.Join(", ", _picker.SelectedIndices)}] · opened {_opened}, closed {_closed}");
}
