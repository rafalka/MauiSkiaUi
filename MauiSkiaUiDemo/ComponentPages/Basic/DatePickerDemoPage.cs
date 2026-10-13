using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiDatePicker"/> (C7) next to MAUI's <see cref="DatePicker"/>: MAUI's properties set on both (the date, its range,
/// the field's format), and SkiaUi's own: the wheels' format (a month and year picker, numbers or names), headers, looping,
/// days that cannot be chosen (weekends, a blackout list), the date the wheels show while there is none, OK / Cancel and clearing.
/// Turn a wheel to a month without the chosen day, or before the minimum: it moves to the nearest day that can be chosen.
/// </summary>
public sealed class DatePickerDemoPage : ComponentDemoPage
{
    private const string CultureWheels = "(culture's)";
    private const string NumberWheels = "dd MM yyyy";
    private const string MonthYear = "MMMM yyyy";
    private const string Expiry = "MM/yy";
    private const string ShortFormat = "d";
    private const string LongFormat = "D";
    private const string CustomFormat = "ddd, dd MMM yyyy";
    private const string NoRange = "1900 to 2100 (MAUI's)";
    private const string ThisYear = "This year";
    private const string NextNinetyDays = "Today and the next 90 days";
    private const string EveryDay = "Every day";
    private const string Weekdays = "Weekdays only (DateEnabled)";
    private const string Holidays = "The 1st and 15th of each month (BlackoutDates)";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";

    private readonly SkUiDatePicker _skia;
    private readonly DatePicker _native;
    private int _opened;
    private int _closed;

    public DatePickerDemoPage()
        : base(nameof(SkUiDatePicker), new SkUiDatePicker { Title = "Due date" }, new DatePicker(), widthRange: (100, 360, 240), heightRange: (30, 120, 48))
    {
        _skia = (SkUiDatePicker)SkiaControl;
        _native = (DatePicker)NativeControl!;
        _skia.DateSelected += (_, _) => Status();
        _native.DateSelected += (_, _) => Status();
        _skia.Opened += (_, _) => { _opened++; Status(); };
        _skia.Closed += (_, _) => { _closed++; Status(); };

        Choice(nameof(DatePicker.Format), [ShortFormat, LongFormat, CustomFormat, Expiry], ShortFormat,
            value => { _skia.Format = value; _native.Format = value; }, () => _skia.Format, () => _native.Format);
        Choice("Range (MinimumDate / MaximumDate)", [NoRange, ThisYear, NextNinetyDays], NoRange, SetRange, RangeName);
        Choice(nameof(SkUiDatePicker.WheelFormat), [CultureWheels, NumberWheels, MonthYear, Expiry], CultureWheels,
            value => _skia.WheelFormat = value == CultureWheels ? null : value, () => _skia.WheelFormat ?? CultureWheels);
        Choice("Days that can be chosen", [EveryDay, Weekdays, Holidays], EveryDay, SetDays, DaysName);
        Toggle(nameof(SkUiDatePicker.AreColumnHeadersVisible), false, value => _skia.AreColumnHeadersVisible = value, () => _skia.AreColumnHeadersVisible);
        Toggle(nameof(SkUiDatePicker.IsLooping), true, value => _skia.IsLooping = value, () => _skia.IsLooping);
        Number(nameof(SkUiDatePicker.VisibleRows), 1, 9, 5, value => _skia.VisibleRows = (int)value, () => _skia.VisibleRows, whole: true);
        ColorEditor(nameof(DatePicker.TextColor), DemoColors.Ink, value => { _skia.TextColor = value; _native.TextColor = value; },
            () => _skia.TextColor ?? DemoColors.Ink, () => _native.TextColor ?? DemoColors.Ink);
        Number(nameof(DatePicker.FontSize), 10, 30, 16, value => { _skia.FontSize = value; _native.FontSize = value; }, () => _skia.FontSize, () => _native.FontSize);
        Choice(nameof(SkUiDatePicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _skia.PopupPlacement = value, () => _skia.PopupPlacement);
        Choice(nameof(SkUiDatePicker.PopupPresenter), [ToolkitPresenter, AppPresenter], ToolkitPresenter,
            value => _skia.PopupPresenter = value == ToolkitPresenter ? SkUiToolkitPopupPresenter.Instance : null,
            () => _skia.PopupPresenter is SkUiToolkitPopupPresenter ? ToolkitPresenter : AppPresenter);
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _skia.UpdateMode = value, () => _skia.UpdateMode);
        Toggle(nameof(SkUiDatePicker.IsClearButtonVisible), false, value => _skia.IsClearButtonVisible = value, () => _skia.IsClearButtonVisible);
        Toggle(nameof(SkUiDatePicker.IsPopupClearButtonVisible), false, value => _skia.IsPopupClearButtonVisible = value, () => _skia.IsPopupClearButtonVisible);
        Text(nameof(SkUiDatePicker.Placeholder), "Pick a date", value => _skia.Placeholder = value, () => _skia.Placeholder ?? "");
        ActionButton("No date (Date = null); wheels show 1 Jan 1990 (DefaultHighlightedDate)", () =>
        {
            _skia.DefaultHighlightedDate = new DateTime(1990, 1, 1);
            _skia.Date = null;
            _native.Date = null;
        });
        ActionButton("Today", () => { _skia.Date = DateTime.Today; _native.Date = DateTime.Today; });
        ActionButton("Open (IsOpen = true)", () => _skia.IsOpen = true);
        _skia.Placeholder = "Pick a date";
        Status();
    }

    private void SetRange(string name)
    {
        var (minimum, maximum) = name switch
        {
            ThisYear => (new DateTime(DateTime.Today.Year, 1, 1), new DateTime(DateTime.Today.Year, 12, 31)),
            NextNinetyDays => (DateTime.Today, DateTime.Today.AddDays(90)),
            _ => (new DateTime(1900, 1, 1), new DateTime(2100, 12, 31))
        };
        // Widen first, so the new range is never inverted on the way.
        _skia.MinimumDate = new DateTime(1900, 1, 1);
        _skia.MaximumDate = maximum;
        _skia.MinimumDate = minimum;
        _native.MinimumDate = new DateTime(1900, 1, 1);
        _native.MaximumDate = maximum;
        _native.MinimumDate = minimum;
    }

    private string RangeName() => _skia.MinimumDate == DateTime.Today ? NextNinetyDays : _skia.MinimumDate?.Year == DateTime.Today.Year ? ThisYear : NoRange;

    private void SetDays(string name)
    {
        _skia.DateEnabled = name == Weekdays ? date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) : null;
        _skia.BlackoutDates = name == Holidays
            ? [.. Enumerable.Range(0, 24).SelectMany(month => new[] { 1, 15 }.Select(day => new DateTime(DateTime.Today.Year, 1, day).AddMonths(month)))]
            : [];
    }

    private string DaysName() => _skia.DateEnabled is not null ? Weekdays : _skia.BlackoutDates is { Count: > 0 } ? Holidays : EveryDay;

    private void Status() => Feedback($"Date {_skia.Date?.ToString("d") ?? "null"} · opened {_opened}, closed {_closed}", $"Date {_native.Date?.ToString("d") ?? "null"}");
}
