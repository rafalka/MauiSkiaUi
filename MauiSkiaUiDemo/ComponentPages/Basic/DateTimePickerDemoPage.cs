using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiDateTimePicker"/> (C7): a day and a time on one popup of wheels, as Syncfusion's <c>SfDateTimePicker</c> and
/// Telerik's <c>RadDateTimePicker</c> (MAUI has none). An appointment from now on: the range starts at the next quarter hour, the
/// times before it on the first day are dimmed, weekends can be blocked, and the minutes come in steps.
/// </summary>
public sealed class DateTimePickerDemoPage : ComponentDemoPage
{
    private const string CultureWheels = "(culture's)";
    private const string NoYear = "dd MMM HH:mm";
    private const string WithSeconds = "dd MM yyyy HH:mm:ss";
    private const string ShortFormat = "g";
    private const string LongFormat = "f";
    private const string NoRange = "1900 to 2100";
    private const string FromNow = "From now, for two weeks";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";

    private readonly SkUiDateTimePicker _skia;
    private int _opened;
    private int _closed;

    public DateTimePickerDemoPage()
        : base(nameof(SkUiDateTimePicker), new SkUiDateTimePicker { Title = "Appointment", Placeholder = "When?" }, widthRange: (100, 360, 260),
            heightRange: (30, 120, 48))
    {
        _skia = (SkUiDateTimePicker)SkiaControl;
        _skia.DateSelected += (_, _) => Status();
        _skia.Opened += (_, _) => { _opened++; Status(); };
        _skia.Closed += (_, _) => { _closed++; Status(); };

        Choice(nameof(SkUiDateTimePicker.Format), [ShortFormat, LongFormat], ShortFormat, value => _skia.Format = value, () => _skia.Format);
        Choice(nameof(SkUiDateTimePicker.WheelFormat), [CultureWheels, NoYear, WithSeconds], CultureWheels,
            value => _skia.WheelFormat = value == CultureWheels ? null : value, () => _skia.WheelFormat ?? CultureWheels);
        Choice("Range (MinimumDate / MaximumDate)", [NoRange, FromNow], NoRange, SetRange, () => _skia.MinimumDate?.Year == 1900 ? NoRange : FromNow);
        Choice(nameof(SkUiDateTimePicker.MinuteInterval), [1, 5, 15, 30], 1, value => _skia.MinuteInterval = value, () => _skia.MinuteInterval);
        Toggle("Weekdays only (DateEnabled)", false,
            value => _skia.DateEnabled = value ? date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) : null,
            () => _skia.DateEnabled is not null);
        Toggle(nameof(SkUiDateTimePicker.AreColumnHeadersVisible), false, value => _skia.AreColumnHeadersVisible = value, () => _skia.AreColumnHeadersVisible);
        Number(nameof(SkUiDateTimePicker.FontSize), 10, 30, 16, value => _skia.FontSize = value, () => _skia.FontSize);
        Choice(nameof(SkUiDateTimePicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _skia.PopupPlacement = value, () => _skia.PopupPlacement);
        Choice(nameof(SkUiDateTimePicker.PopupPresenter), [ToolkitPresenter, AppPresenter], ToolkitPresenter,
            value => _skia.PopupPresenter = value == ToolkitPresenter ? SkUiToolkitPopupPresenter.Instance : null,
            () => _skia.PopupPresenter is SkUiToolkitPopupPresenter ? ToolkitPresenter : AppPresenter);
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _skia.UpdateMode = value, () => _skia.UpdateMode);
        Toggle(nameof(SkUiDateTimePicker.IsClearButtonVisible), false, value => _skia.IsClearButtonVisible = value, () => _skia.IsClearButtonVisible);
        ActionButton("Clear (Date = null)", () => _skia.Date = null);
        ActionButton("Open (IsOpen = true)", () => _skia.IsOpen = true);
        Status();
    }

    private void SetRange(string name)
    {
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddMinutes((now.Minute / 15 + 1) * 15);
        var (minimum, maximum) = name == FromNow ? (next, next.AddDays(14)) : (new DateTime(1900, 1, 1), new DateTime(2100, 12, 31, 23, 59, 59));
        _skia.MinimumDate = new DateTime(1900, 1, 1);
        _skia.MaximumDate = maximum;
        _skia.MinimumDate = minimum;
    }

    private void Status() => Feedback($"Date {_skia.Date?.ToString("g") ?? "null"} · opened {_opened}, closed {_closed}");
}
