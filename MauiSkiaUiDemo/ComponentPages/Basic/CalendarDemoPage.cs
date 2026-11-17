using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// <see cref="SkUiCalendar"/>: a drawn calendar with month, year, decade and century pages. Swipe or use the header's buttons
/// to change the month, tap the title to zoom out a level (or pinch), tap a month or year to zoom back in. The editors cover
/// the selection modes, the day rules, the header and day-name options, the weekly strip and the colors; the presets show a
/// compact styled calendar, a range booking and days with events (the preset buttons reset what the editors set).
/// </summary>
public sealed class CalendarDemoPage : ComponentDemoPage
{
    private const string Plain = "Plain";
    private const string Styled = "Compact styled header";
    private const string Booking = "Range booking";
    private const string Events = "Events and holidays";

    private static readonly Color Gray100 = Color.FromArgb("#F3F4F6");
    private static readonly Color Gray500 = Color.FromArgb("#6B7280");
    private static readonly Color Gray600 = Color.FromArgb("#4B5563");
    private static readonly Color Gray900 = Color.FromArgb("#111827");

    private readonly SkUiCalendar _calendar;
    private int _changes;

    public CalendarDemoPage()
        : base(nameof(SkUiCalendar), new SkUiCalendar(), widthRange: (220, 420, 340), heightRange: (120, 520, 380))
    {
        _calendar = (SkUiCalendar)SkiaControl;
        _calendar.SelectionChanged += (_, _) => { _changes++; Status(); };
        _calendar.DateTapped += (_, args) => { if (!args.IsEnabled) Feedback($"{args.Date:d} cannot be chosen"); };
        // A tap on a week number selects the week's days that can be chosen.
        _calendar.WeekNumberTapped += (_, args) =>
        {
            if (_calendar.SelectionMode == SkUiCalendarSelectionMode.Range)
                _calendar.SelectedRange = args.EnabledRange;
            else if (_calendar.SelectionMode == SkUiCalendarSelectionMode.Multiple && args.EnabledRange is { } week)
                _calendar.SelectedDates = [.. Enumerable.Range(0, week.Days).Select(offset => week.Start.AddDays(offset)).Where(day => _calendar.DateEnabled?.Invoke(day) ?? true)];
            Feedback($"Week {args.WeekNumber}: {args.WeekStart:d} – {args.WeekEnd:d}");
        };
        _calendar.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SkUiCalendar.DisplayDate) or nameof(SkUiCalendar.DisplayMode))
                Status();
        };

        Choice(nameof(SkUiCalendar.SelectionMode), Enum.GetValues<SkUiCalendarSelectionMode>(), SkUiCalendarSelectionMode.Single,
            value => _calendar.SelectionMode = value, () => _calendar.SelectionMode);
        Toggle(nameof(SkUiCalendar.AllowsDeselection), false, value => _calendar.AllowsDeselection = value, () => _calendar.AllowsDeselection);
        Choice(nameof(SkUiCalendar.MinimumDisplayMode), Enum.GetValues<SkUiCalendarDisplayMode>(), SkUiCalendarDisplayMode.Month,
            value => _calendar.MinimumDisplayMode = value, () => _calendar.MinimumDisplayMode);
        Choice(nameof(SkUiCalendar.MaximumDisplayMode), Enum.GetValues<SkUiCalendarDisplayMode>(), SkUiCalendarDisplayMode.Century,
            value => _calendar.MaximumDisplayMode = value, () => _calendar.MaximumDisplayMode);
        Number(nameof(SkUiCalendar.VisibleWeeks), 1, 6, 6, value => _calendar.VisibleWeeks = (int)value, () => _calendar.VisibleWeeks, whole: true);
        Choice(nameof(SkUiCalendar.FirstDayOfWeek), ["Culture", .. Enum.GetNames<DayOfWeek>()], "Culture",
            value => _calendar.FirstDayOfWeek = value == "Culture" ? null : Enum.Parse<DayOfWeek>(value),
            () => _calendar.FirstDayOfWeek?.ToString() ?? "Culture");
        Choice(nameof(SkUiCalendar.RangeSelectionTarget), Enum.GetValues<SkUiCalendarRangeTarget>(), SkUiCalendarRangeTarget.Auto,
            value => _calendar.RangeSelectionTarget = value, () => _calendar.RangeSelectionTarget);
        Number(nameof(SkUiCalendar.MinimumRangeDays), 0, 14, 0, value => _calendar.MinimumRangeDays = (int)value, () => _calendar.MinimumRangeDays, whole: true);
        Number(nameof(SkUiCalendar.MaximumRangeDays), 0, 60, 0, value => _calendar.MaximumRangeDays = (int)value, () => _calendar.MaximumRangeDays, whole: true);
        Toggle(nameof(SkUiCalendar.AllowsDisabledDaysInRange), true, value => _calendar.AllowsDisabledDaysInRange = value,
            () => _calendar.AllowsDisabledDaysInRange);
        Toggle("Range: from today", false, value => _calendar.MinimumDate = value ? DateTime.Today : null, () => _calendar.MinimumDate is not null);
        Toggle("Weekdays only (DateEnabled)", false,
            value => _calendar.DateEnabled = value ? date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) : null,
            () => _calendar.DateEnabled is not null);
        Choice(nameof(SkUiCalendar.HeaderLayout), Enum.GetValues<SkUiCalendarHeaderLayout>(), SkUiCalendarHeaderLayout.TitleFirst,
            value => _calendar.HeaderLayout = value, () => _calendar.HeaderLayout);
        Toggle(nameof(SkUiCalendar.IsHeaderVisible), true, value => _calendar.IsHeaderVisible = value, () => _calendar.IsHeaderVisible);
        Toggle(nameof(SkUiCalendar.AreDayNamesVisible), true, value => _calendar.AreDayNamesVisible = value, () => _calendar.AreDayNamesVisible);
        Choice(nameof(SkUiCalendar.DayNameFormat), Enum.GetValues<SkUiCalendarDayNameFormat>(), SkUiCalendarDayNameFormat.Shortest,
            value => _calendar.DayNameFormat = value, () => _calendar.DayNameFormat);
        Toggle(nameof(SkUiCalendar.IsTodayDayNameHighlighted), true, value => _calendar.IsTodayDayNameHighlighted = value,
            () => _calendar.IsTodayDayNameHighlighted);
        Toggle(nameof(SkUiCalendar.AreAdjacentDaysVisible), true, value => _calendar.AreAdjacentDaysVisible = value, () => _calendar.AreAdjacentDaysVisible);
        Toggle(nameof(SkUiCalendar.AreWeekNumbersVisible), false, value => _calendar.AreWeekNumbersVisible = value, () => _calendar.AreWeekNumbersVisible);
        Choice(nameof(SkUiCalendar.NavigationOrientation), [StackOrientation.Horizontal, StackOrientation.Vertical], StackOrientation.Horizontal,
            value => _calendar.NavigationOrientation = value, () => _calendar.NavigationOrientation);
        Toggle(nameof(SkUiCalendar.IsSwipeEnabled), true, value => _calendar.IsSwipeEnabled = value, () => _calendar.IsSwipeEnabled);
        Choice(nameof(SkUiCalendar.TodayStyle), Enum.GetValues<SkUiCalendarTodayStyle>(), SkUiCalendarTodayStyle.Ring,
            value => _calendar.TodayStyle = value, () => _calendar.TodayStyle);
        Choice(nameof(SkUiCalendar.SelectionShape), Enum.GetValues<SkUiCalendarSelectionShape>(), SkUiCalendarSelectionShape.Circle,
            value => _calendar.SelectionShape = value, () => _calendar.SelectionShape);
        ColorEditor(nameof(SkUiCalendar.SelectionColor), Accent, value => _calendar.SelectionColor = value, () => _calendar.SelectionColor ?? Accent);
        ColorEditor(nameof(SkUiCalendar.TodayColor), Accent, value => _calendar.TodayColor = value, () => _calendar.TodayColor ?? Accent);
        Number("FontSize (0 = look's)", 0, 24, 0, value => _calendar.FontSize = value <= 0 ? double.NaN : value,
            () => double.IsNaN(_calendar.FontSize) ? 0 : _calendar.FontSize);
        ActionButton(Plain, () => ApplyPreset(Plain));
        ActionButton(Styled, () => ApplyPreset(Styled));
        ActionButton(Booking, () => ApplyPreset(Booking));
        ActionButton(Events, () => ApplyPreset(Events));
        ActionButton("Previous", () => _calendar.GoToPrevious());
        ActionButton("Next", () => _calendar.GoToNext());
        ActionButton("Today", () => _calendar.GoToToday());
        ActionButton("Clear selection", _calendar.ClearSelection);
        Status();
    }

    private void ApplyPreset(string preset)
    {
        Reset();
        switch (preset)
        {
            case Styled:
                // A compact header (title first, round gray buttons), a semibold gray weekday row, today as a ring with a
                // semibold number, selected days in the accent with white numbers.
                _calendar.HeaderLayout = SkUiCalendarHeaderLayout.TitleFirst;
                _calendar.HeaderTextColor = Gray900;
                _calendar.HeaderFontSize = 18;
                _calendar.NavigationButtonColor = Gray600;
                _calendar.NavigationButtonBackgroundColor = Gray100;
                _calendar.NavigationButtonSize = 32;
                _calendar.NavigationButtonSpacing = 16;
                _calendar.HeaderPadding = new Thickness(12, 0);
                _calendar.DayNameFormat = SkUiCalendarDayNameFormat.Abbreviated;
                _calendar.DayNameTextColor = Gray500;
                _calendar.DayNameFontAttributes = FontAttributes.Bold;
                _calendar.TextColor = Gray900;
                _calendar.FontSize = 15;
                _calendar.TodayStyle = SkUiCalendarTodayStyle.Ring;
                _calendar.TodayTextColor = Gray900;
                _calendar.SelectedTextColor = Colors.White;
                _calendar.FirstDayOfWeek = DayOfWeek.Monday;
                break;
            case Booking:
                _calendar.SelectionMode = SkUiCalendarSelectionMode.Range;
                _calendar.MinimumDate = DateTime.Today;
                _calendar.MaximumDate = DateTime.Today.AddMonths(6);
                _calendar.HeaderLayout = SkUiCalendarHeaderLayout.Centered;
                _calendar.MaximumDisplayMode = SkUiCalendarDisplayMode.Year;
                foreach (var offset in new[] { 3, 4, 11 })
                    _calendar.BlackoutDates!.Add(DateTime.Today.AddDays(offset));
                break;
            case Events:
                _calendar.SelectionMode = SkUiCalendarSelectionMode.Multiple;
                _calendar.FirstDayOfWeek = DayOfWeek.Monday;
                _calendar.DayNameFormat = SkUiCalendarDayNameFormat.FirstLetter;
                _calendar.DayStyle = EventStyle;
                break;
        }
        Status();
    }

    private void Reset()
    {
        foreach (var property in new[]
                 {
                     SkUiCalendar.SelectionModeProperty, SkUiCalendar.MinimumDateProperty, SkUiCalendar.MaximumDateProperty,
                     SkUiCalendar.HeaderLayoutProperty, SkUiCalendar.HeaderTextColorProperty, SkUiCalendar.HeaderFontSizeProperty,
                     SkUiCalendar.NavigationButtonColorProperty, SkUiCalendar.NavigationButtonBackgroundColorProperty,
                     SkUiCalendar.NavigationButtonSizeProperty, SkUiCalendar.NavigationButtonSpacingProperty, SkUiCalendar.HeaderPaddingProperty,
                     SkUiCalendar.DayNameFormatProperty, SkUiCalendar.DayNameTextColorProperty, SkUiCalendar.DayNameFontAttributesProperty,
                     SkUiCalendar.TextColorProperty, SkUiCalendar.FontSizeProperty, SkUiCalendar.TodayStyleProperty,
                     SkUiCalendar.TodayTextColorProperty, SkUiCalendar.SelectedTextColorProperty, SkUiCalendar.FirstDayOfWeekProperty,
                     SkUiCalendar.MaximumDisplayModeProperty, SkUiCalendar.SelectionShapeProperty, SkUiCalendar.WeekendTextColorProperty,
                     SkUiCalendar.RangeSelectionTargetProperty
                 })
            _calendar.ClearValue(property);
        _calendar.BlackoutDates?.Clear();
        _calendar.DayStyle = null;
        _calendar.ClearSelection();
    }

    private static readonly Color Shift = Color.FromArgb("#0E7490");
    private static readonly Color TimeOff = Color.FromArgb("#8B5CF6");
    private static readonly Color Vacation = Color.FromArgb("#D97706");

    /// <summary>
    /// Sample events keyed on the day of the month: shifts, time off and vacation as bars (a legend under a month), birthdays as
    /// a glyph, holidays as a caption and a tint, a cancelled day struck through.
    /// </summary>
    private static SkUiCalendarDayStyle? EventStyle(DateTime day) => day.Day switch
    {
        2 or 9 or 16 or 23 or 30 or 4 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Bar(Shift)] },
        5 or 12 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Bar(TimeOff)] },
        26 or 27 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Bar(Vacation)] },
        7 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Glyph("★", color: Vacation), SkUiCalendarMarker.Dot(Shift)] },
        14 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Caption("Holiday", Colors.Crimson)], BackgroundColor = Color.FromArgb("#FEE2E2") },
        19 => new SkUiCalendarDayStyle { Markers = [SkUiCalendarMarker.Dot(Shift), SkUiCalendarMarker.Dot(TimeOff), SkUiCalendarMarker.Dot(Vacation)] },
        21 => new SkUiCalendarDayStyle { IsStrikethrough = true, TextColor = Colors.Gray },
        _ => null
    };

    private void Status()
    {
        var culture = CultureInfo.CurrentCulture;
        var selection = _calendar.SelectionMode switch
        {
            SkUiCalendarSelectionMode.Multiple => $"{_calendar.SelectedDates.Count} days",
            SkUiCalendarSelectionMode.MultipleRanges => $"{_calendar.SelectedRanges.Count} ranges, {_calendar.SelectedRanges.Sum(range => range.Days)} days",
            SkUiCalendarSelectionMode.Range when _calendar.SelectedRange is { } range =>
                range.End is { } end ? $"{range.Start.ToString("d", culture)} – {end.ToString("d", culture)} ({range.Days} days)"
                    : $"from {range.Start.ToString("d", culture)}, choose the end",
            SkUiCalendarSelectionMode.None => "no selection",
            _ => _calendar.SelectedDate?.ToString("D", culture) ?? "nothing chosen"
        };
        Feedback($"{_calendar.Title} · {_calendar.DisplayMode} · {selection} · changes {_changes}");
    }
}
