using MauiSkiaUi;
using MauiSkiaUi.CommunityToolkit;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiTimePicker"/> (C7) next to MAUI's <see cref="TimePicker"/>: MAUI's properties set on both (the time, the field's
/// format), and SkiaUi's own: the clock of the wheels (the culture's, 12 or 24 hours, seconds),
/// steps, a range (opening hours), the time the wheels show while there is none, OK / Cancel and clearing.
/// </summary>
public sealed class TimePickerDemoPage : ComponentDemoPage
{
    private const string CultureWheels = "(culture's)";
    private const string TwentyFour = "HH:mm";
    private const string Twelve = "h:mm tt";
    private const string Seconds = "HH:mm:ss";
    private const string ShortFormat = "t";
    private const string LongFormat = "T";
    private const string AllDay = "All day";
    private const string OpeningHours = "Opening hours 9:00–17:30";
    private const string ToolkitPresenter = "CommunityToolkit Popup";
    private const string AppPresenter = "App's (SkUiPopups.Presenter)";

    private readonly SkUiTimePicker _skia;
    private readonly TimePicker _native;
    private int _opened;
    private int _closed;

    public TimePickerDemoPage()
        : base(nameof(SkUiTimePicker), new SkUiTimePicker { Title = "Alarm" }, new TimePicker(), widthRange: (100, 360, 200), heightRange: (30, 120, 48))
    {
        _skia = (SkUiTimePicker)SkiaControl;
        _native = (TimePicker)NativeControl!;
        _skia.TimeSelected += (_, _) => Status();
        _native.TimeSelected += (_, _) => Status();
        _skia.Opened += (_, _) => { _opened++; Status(); };
        _skia.Closed += (_, _) => { _closed++; Status(); };

        Choice(nameof(TimePicker.Format), [ShortFormat, LongFormat, TwentyFour, Twelve], ShortFormat,
            value => { _skia.Format = value; _native.Format = value; }, () => _skia.Format, () => _native.Format);
        Choice(nameof(SkUiTimePicker.WheelFormat), [CultureWheels, TwentyFour, Twelve, Seconds], CultureWheels,
            value => _skia.WheelFormat = value == CultureWheels ? null : value, () => _skia.WheelFormat ?? CultureWheels);
        Choice(nameof(SkUiTimePicker.MinuteInterval), [1, 5, 10, 15, 30], 1, value => _skia.MinuteInterval = value, () => _skia.MinuteInterval);
        Choice(nameof(SkUiTimePicker.HourInterval), [1, 2, 3, 6], 1, value => _skia.HourInterval = value, () => _skia.HourInterval);
        Choice(nameof(SkUiTimePicker.SecondInterval), [1, 5, 15, 30], 1, value => _skia.SecondInterval = value, () => _skia.SecondInterval);
        Choice("Range (MinimumTime / MaximumTime)", [AllDay, OpeningHours], AllDay, SetRange,
            () => _skia.MinimumTime == TimeSpan.Zero ? AllDay : OpeningHours);
        Toggle(nameof(SkUiTimePicker.AreColumnHeadersVisible), false, value => _skia.AreColumnHeadersVisible = value, () => _skia.AreColumnHeadersVisible);
        Toggle(nameof(SkUiTimePicker.IsLooping), true, value => _skia.IsLooping = value, () => _skia.IsLooping);
        ColorEditor(nameof(TimePicker.TextColor), DemoColors.Ink, value => { _skia.TextColor = value; _native.TextColor = value; },
            () => _skia.TextColor ?? DemoColors.Ink, () => _native.TextColor ?? DemoColors.Ink);
        Number(nameof(TimePicker.FontSize), 10, 30, 16, value => { _skia.FontSize = value; _native.FontSize = value; }, () => _skia.FontSize, () => _native.FontSize);
        Choice(nameof(SkUiTimePicker.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _skia.PopupPlacement = value, () => _skia.PopupPlacement);
        Choice(nameof(SkUiTimePicker.PopupPresenter), [ToolkitPresenter, AppPresenter], ToolkitPresenter,
            value => _skia.PopupPresenter = value == ToolkitPresenter ? SkUiToolkitPopupPresenter.Instance : null,
            () => _skia.PopupPresenter is SkUiToolkitPopupPresenter ? ToolkitPresenter : AppPresenter);
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _skia.UpdateMode = value, () => _skia.UpdateMode);
        Toggle(nameof(SkUiTimePicker.IsClearButtonVisible), false, value => _skia.IsClearButtonVisible = value, () => _skia.IsClearButtonVisible);
        Toggle(nameof(SkUiTimePicker.IsPopupClearButtonVisible), false, value => _skia.IsPopupClearButtonVisible = value, () => _skia.IsPopupClearButtonVisible);
        ActionButton("No time (Time = null); wheels show 7:00 (DefaultHighlightedTime)", () =>
        {
            _skia.DefaultHighlightedTime = new TimeSpan(7, 0, 0);
            _skia.Time = null;
            _native.Time = null;
        });
        ActionButton("Now", () => { _skia.Time = DateTime.Now.TimeOfDay; _native.Time = DateTime.Now.TimeOfDay; });
        ActionButton("Open (IsOpen = true)", () => _skia.IsOpen = true);
        Status();
    }

    private void SetRange(string name)
    {
        var (minimum, maximum) = name == OpeningHours ? (new TimeSpan(9, 0, 0), new TimeSpan(17, 30, 0)) : (TimeSpan.Zero, new TimeSpan(23, 59, 59));
        // Widen first, so the new range is never inverted on the way.
        _skia.MinimumTime = TimeSpan.Zero;
        _skia.MaximumTime = maximum;
        _skia.MinimumTime = minimum;
    }

    private void Status() => Feedback($"Time {_skia.Time?.ToString(@"hh\:mm\:ss") ?? "null"} · opened {_opened}, closed {_closed}",
        $"Time {_native.Time?.ToString(@"hh\:mm\:ss") ?? "null"}");
}
