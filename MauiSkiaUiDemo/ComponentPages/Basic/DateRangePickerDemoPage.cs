using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiDateRangePicker"/>: a field showing a run of days whose popup is a month calendar in range selection. Tap a first
/// day, then a last day: the popup closes with the range (or keeps it as a draft until OK with <c>UpdateMode</c>
/// <c>WhenFinished</c>). The editors cover the field's format (<c>DisplayFormat</c>), the days that can be chosen and the range
/// rules: the shortest and longest range, and whether a range may run over a day that cannot be chosen.
/// </summary>
public sealed class DateRangePickerDemoPage : ComponentDemoPage
{
    private const string ShortFormat = SkUiDateRangePicker.DefaultDisplayFormat;
    private const string MonthDay = "{0:MMM d} – {1:MMM d}";
    private const string WithDays = "{0:ddd d MMM} → {1:ddd d MMM} ({2} days)";
    private const string EveryDay = "Every day";
    private const string FromToday = "Today and later (MinimumDate)";
    private const string Booked = "Booked nights on the 10th and 20th (BlackoutDates)";

    private readonly SkUiDateRangePicker _picker;
    private int _changes;

    public DateRangePickerDemoPage()
        : base(nameof(SkUiDateRangePicker), new SkUiDateRangePicker { Title = "Stay", Placeholder = "Pick your dates" }, widthRange: (140, 400, 300),
            heightRange: (30, 120, 48))
    {
        _picker = (SkUiDateRangePicker)SkiaControl;
        _picker.ValueChanged += (_, _) => { _changes++; Status(); };

        Choice(nameof(SkUiDateRangePicker.DisplayFormat), [ShortFormat, MonthDay, WithDays], ShortFormat, value => _picker.DisplayFormat = value,
            () => _picker.DisplayFormat);
        Choice("Days that can be chosen", [EveryDay, FromToday, Booked], EveryDay, SetDays, DaysName);
        Number(nameof(SkUiDateRangePicker.MinimumRangeDays), 0, 14, 0, value => _picker.MinimumRangeDays = (int)value, () => _picker.MinimumRangeDays, whole: true);
        Number(nameof(SkUiDateRangePicker.MaximumRangeDays), 0, 60, 0, value => _picker.MaximumRangeDays = (int)value, () => _picker.MaximumRangeDays, whole: true);
        Toggle(nameof(SkUiDateRangePicker.AllowsDisabledDaysInRange), true, value => _picker.AllowsDisabledDaysInRange = value,
            () => _picker.AllowsDisabledDaysInRange);
        Choice(nameof(SkUiPickerBase.UpdateMode), [SkUiPickerUpdateMode.Immediately, SkUiPickerUpdateMode.WhenFinished], SkUiPickerUpdateMode.Immediately,
            value => _picker.UpdateMode = value, () => _picker.UpdateMode);
        Choice(nameof(SkUiPickerBase.PopupPlacement), Enum.GetValues<SkUiPopupPlacement>(), SkUiPopupPlacement.Default,
            value => _picker.PopupPlacement = value, () => _picker.PopupPlacement);
        Toggle(nameof(SkUiPickerBase.IsClearButtonVisible), false, value => _picker.IsClearButtonVisible = value, () => _picker.IsClearButtonVisible);
        Toggle(nameof(SkUiPickerBase.IsPopupClearButtonVisible), false, value => _picker.IsPopupClearButtonVisible = value, () => _picker.IsPopupClearButtonVisible);
        ActionButton("This week (Value)", () =>
        {
            var start = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            _picker.Value = new SkUiDateRange(start, start.AddDays(6));
        });
        ActionButton("Open (IsOpen = true)", () => _picker.IsOpen = true);
        Status();
    }

    private void SetDays(string name)
    {
        _picker.MinimumDate = name == FromToday ? DateTime.Today : null;
        _picker.BlackoutDates = name == Booked
            ? [.. Enumerable.Range(-1, 14).SelectMany(month => new[] { 10, 20 }.Select(day => new DateTime(DateTime.Today.Year, DateTime.Today.Month, day).AddMonths(month)))]
            : [];
    }

    private string DaysName() => _picker.MinimumDate is not null ? FromToday : _picker.BlackoutDates is { Count: > 0 } ? Booked : EveryDay;

    private void Status() => Feedback(_picker.Value is { } range ? $"{range.Start:d} – {range.LastDay:d} ({range.Days} days) · changes {_changes}" : $"No range · changes {_changes}");
}
