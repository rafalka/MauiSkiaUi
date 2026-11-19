using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What a calendar's page shows (<see cref="SkUiCalendar.DisplayMode"/>).</summary>
public enum SkUiCalendarDisplayMode
{
    /// <summary>The days of a month (or of one to five weeks, <see cref="SkUiCalendar.VisibleWeeks"/>).</summary>
    Month,
    /// <summary>The twelve months of a year.</summary>
    Year,
    /// <summary>The years of a decade, with the last year before and the first year after it.</summary>
    Decade,
    /// <summary>The decades of a century, with the last decade before and the first decade after it.</summary>
    Century
}

/// <summary>How many days a calendar lets the user choose (<see cref="SkUiCalendar.SelectionMode"/>).</summary>
public enum SkUiCalendarSelectionMode
{
    /// <summary>None: taps raise <see cref="SkUiCalendar.DateTapped"/> only.</summary>
    None,
    /// <summary>One day, <see cref="SkUiCalendar.SelectedDate"/>.</summary>
    Single,
    /// <summary>Any number of days, <see cref="SkUiCalendar.SelectedDates"/>: a tap adds a day or removes it.</summary>
    Multiple,
    /// <summary>A run of days, <see cref="SkUiCalendar.SelectedRange"/>: the first tap starts it, the second ends it, a third starts a new one.</summary>
    Range,
    /// <summary>
    /// Several runs of days, <see cref="SkUiCalendar.SelectedRanges"/>: a tap outside them starts a range, the next tap ends it
    /// (ranges that overlap or touch join into one), and a tap on a chosen range removes it.
    /// <see cref="SkUiCalendar.SelectedRange"/> is the range changed last.
    /// </summary>
    MultipleRanges
}

/// <summary>What a date picker's popup shows (<see cref="SkUiDatePicker.PopupStyle"/>).</summary>
public enum SkUiDatePickerPopupStyle
{
    /// <summary>The look's choice (<see cref="SkUiLook.DatePickerPopupStyle"/>; wheels on every built-in look).</summary>
    Default,
    /// <summary>Wheels for the day, the month and the year (<see cref="SkUiDateTimePickerBase.WheelFormat"/>).</summary>
    Wheels,
    /// <summary>A month calendar (<see cref="SkUiCalendar"/>): a tap on a day chooses it.</summary>
    Calendar
}

/// <summary>
/// Where a calendar header button's chevron points (<see cref="SkUiCalendarNavigationButtonPaint.Direction"/>): left and right while
/// the pages move sideways (mirrored right-to-left), up and down while they move vertically (<c>NavigationOrientation</c>).
/// </summary>
public enum SkUiCalendarChevronDirection
{
    /// <summary>Left: the previous button left-to-right, the next button right-to-left.</summary>
    Left,
    /// <summary>Right: the next button left-to-right, the previous button right-to-left.</summary>
    Right,
    /// <summary>Up: the previous button of a calendar whose pages move vertically.</summary>
    Up,
    /// <summary>Down: the next button of a calendar whose pages move vertically.</summary>
    Down
}

/// <summary>Where a calendar's header puts its title and its previous / next buttons (<see cref="SkUiCalendar.HeaderLayout"/>).</summary>
public enum SkUiCalendarHeaderLayout
{
    /// <summary>The title at the start, both buttons together at the end.</summary>
    TitleFirst,
    /// <summary>The title centered, the previous button before it and the next button after it.</summary>
    Centered
}

/// <summary>How a calendar names the days of the week above its columns (<see cref="SkUiCalendar.DayNameFormat"/>).</summary>
public enum SkUiCalendarDayNameFormat
{
    /// <summary>The culture's shortest names ("Mo", "Tu" in English).</summary>
    Shortest,
    /// <summary>The culture's abbreviated names ("Mon", "Tue" in English).</summary>
    Abbreviated,
    /// <summary>The first letter of the culture's shortest name ("M", "T").</summary>
    FirstLetter
}

/// <summary>How a calendar marks today (<see cref="SkUiCalendar.TodayStyle"/>).</summary>
public enum SkUiCalendarTodayStyle
{
    /// <summary>A ring around the day in <see cref="SkUiCalendar.TodayColor"/>.</summary>
    Ring,
    /// <summary>The day filled with <see cref="SkUiCalendar.TodayColor"/>, its number in a contrasting color.</summary>
    Filled,
    /// <summary>Only the number, in <see cref="SkUiCalendar.TodayColor"/> (or <see cref="SkUiCalendar.TodayTextColor"/>) and <see cref="SkUiCalendar.TodayFontAttributes"/>.</summary>
    Text,
    /// <summary>Not marked.</summary>
    None
}

/// <summary>The shape behind a selected day (<see cref="SkUiCalendar.SelectionShape"/>).</summary>
public enum SkUiCalendarSelectionShape
{
    /// <summary>A circle (a range: a band with round ends).</summary>
    Circle,
    /// <summary>A square with rounded corners.</summary>
    RoundedSquare,
    /// <summary>A square filling the cell.</summary>
    Square
}

/// <summary>Which part of a selected range a day is (<see cref="SkUiCalendarCellPaint.RangePart"/>).</summary>
public enum SkUiCalendarRangePart
{
    /// <summary>Not in a range.</summary>
    None,
    /// <summary>The range's first day.</summary>
    Start,
    /// <summary>A day between the first and the last.</summary>
    Middle,
    /// <summary>The range's last day.</summary>
    End,
    /// <summary>A range of one day.</summary>
    StartAndEnd
}

/// <summary>The kind of cell a look draws (<see cref="SkUiCalendarCellPaint.Kind"/>).</summary>
public enum SkUiCalendarCellKind
{
    /// <summary>A day of the month page.</summary>
    Day,
    /// <summary>A month, a year or a decade of the year, decade and century pages.</summary>
    Period
}

/// <summary>
/// A run of days from <see cref="Start"/> to <see cref="End"/>, both included and both dates without a time; an open range
/// (<see cref="End"/> <c>null</c>) has its first day chosen and waits for its last. Made in either order: the earlier date
/// becomes <see cref="Start"/>.
/// </summary>
public readonly record struct SkUiDateRange
{
    /// <summary>An open range: <paramref name="start"/> chosen, the end not yet (its time dropped).</summary>
    public SkUiDateRange(DateTime start)
    {
        Start = start.Date;
    }

    /// <summary>The range from <paramref name="start"/> to <paramref name="end"/> (times dropped; swapped when <paramref name="end"/> is earlier); <c>null</c> <paramref name="end"/>: open.</summary>
    public SkUiDateRange(DateTime start, DateTime? end)
    {
        start = start.Date;
        var last = end?.Date;
        (Start, End) = last < start ? (last.Value, start) : (start, last);
    }

    /// <summary>The first day.</summary>
    public DateTime Start { get; }

    /// <summary>The last day; <c>null</c> while the range is open (only its first day chosen).</summary>
    public DateTime? End { get; }

    /// <summary>Whether the last day is chosen.</summary>
    public bool IsComplete => End is not null;

    /// <summary>The last day, or <see cref="Start"/> while the range is open.</summary>
    public DateTime LastDay => End ?? Start;

    /// <summary>How many days the range covers (1 for one day or an open range).</summary>
    public int Days => (LastDay - Start).Days + 1;

    /// <summary>Whether <paramref name="date"/>'s day is in the range (an open range holds its first day).</summary>
    public bool Contains(DateTime date)
    {
        var day = date.Date;
        return day >= Start && day <= LastDay;
    }
}

/// <summary>Which end of the range a tap sets in range selection (<see cref="SkUiCalendar.RangeSelectionTarget"/>).</summary>
public enum SkUiCalendarRangeTarget
{
    /// <summary>The calendar decides: the first tap starts a range, the second ends it, a third starts a new one.</summary>
    Auto,
    /// <summary>A tap sets the first day (a last day before it is cleared); then the target moves to <see cref="End"/>.</summary>
    Start,
    /// <summary>A tap sets the last day (one before the first day becomes the first, the old first the last); the target stays.</summary>
    End
}

/// <summary>What a calendar marker under a day is (<see cref="SkUiCalendarMarker.Kind"/>).</summary>
public enum SkUiCalendarMarkerKind
{
    /// <summary>A small dot.</summary>
    Dot,
    /// <summary>A short rounded bar.</summary>
    Bar,
    /// <summary>An icon: a glyph of an icon font (<see cref="SkUiCalendarMarker.Text"/> in <see cref="SkUiCalendarMarker.FontFamily"/>).</summary>
    Glyph,
    /// <summary>A short caption in small text (shortened to the cell's width).</summary>
    Caption,
    /// <summary>
    /// An image (<see cref="SkUiCalendarMarker.Source"/>, 1.15): a PNG, an SVG, a URL, through the shared image loader and cache;
    /// left out until it has loaded.
    /// </summary>
    Image
}

/// <summary>
/// One mark in the area under a day's number (<see cref="SkUiCalendarDayStyle.Markers"/>): a dot, a bar, an icon glyph, a
/// caption or an image, for events such as holidays, birthdays, shifts or time off. The marks of a day sit in one row, centered, in the
/// order given; what does not fit is left out.
/// </summary>
public sealed class SkUiCalendarMarker
{
    /// <summary>What the mark is.</summary>
    public SkUiCalendarMarkerKind Kind { get; init; }

    /// <summary>The mark's color; <c>null</c>: the calendar's text color (on a selected day, the selected text's color).</summary>
    public Color? Color { get; init; }

    /// <summary>The glyph or the caption.</summary>
    public string? Text { get; init; }

    /// <summary>The glyph's or caption's font family (an icon font for a glyph); <c>null</c>: the calendar's.</summary>
    public string? FontFamily { get; init; }

    /// <summary>The image of an <see cref="SkUiCalendarMarkerKind.Image"/> marker (1.15).</summary>
    public SkUiImageSource? Source { get; init; }

    /// <summary>
    /// The image's width and height in DIPs (1.15); 0: as tall as the marker area. Fitted into that square, keeping its aspect.
    /// </summary>
    public double Size { get; init; }

    /// <summary>A dot of <paramref name="color"/>.</summary>
    public static SkUiCalendarMarker Dot(Color? color = null) => new() { Kind = SkUiCalendarMarkerKind.Dot, Color = color };

    /// <summary>A bar of <paramref name="color"/>.</summary>
    public static SkUiCalendarMarker Bar(Color? color = null) => new() { Kind = SkUiCalendarMarkerKind.Bar, Color = color };

    /// <summary>The icon <paramref name="glyph"/> of the font <paramref name="fontFamily"/>, in <paramref name="color"/>.</summary>
    public static SkUiCalendarMarker Glyph(string glyph, string? fontFamily = null, Color? color = null) =>
        new() { Kind = SkUiCalendarMarkerKind.Glyph, Text = glyph, FontFamily = fontFamily, Color = color };

    /// <summary>The caption <paramref name="text"/> in <paramref name="color"/>.</summary>
    public static SkUiCalendarMarker Caption(string text, Color? color = null) => new() { Kind = SkUiCalendarMarkerKind.Caption, Text = text, Color = color };

    /// <summary>
    /// The image <paramref name="source"/> (a MAUI <c>ImageSource</c> or a file name converts), <paramref name="size"/> DIPs square
    /// (0: as tall as the marker area), for avatars or event icons (1.15). Each source is loaded once per calendar however many days
    /// show it, and shared with the rest of the app through the image cache. Tint an SVG with
    /// <see cref="SkUiImageSource.FromVector(SkUiImageSource, Color?, double)"/>; <see cref="Color"/> does not apply.
    /// </summary>
    public static SkUiCalendarMarker Image(SkUiImageSource source, double size = 0)
    {
        ArgumentNullException.ThrowIfNull(source);
        SkUiValidate.ThrowIfNegativeOrNotFinite(size, nameof(size));
        return new() { Kind = SkUiCalendarMarkerKind.Image, Source = source, Size = size };
    }
}

/// <summary>
/// How a calendar draws one day, returned by the app's <see cref="SkUiCalendar.DayStyle"/>: what is not set keeps the
/// calendar's own look. Selection and today are drawn over it.
/// </summary>
public sealed class SkUiCalendarDayStyle
{
    /// <summary>The number's color (selected days keep <see cref="SkUiCalendar.SelectedTextColor"/>).</summary>
    public Color? TextColor { get; init; }

    /// <summary>A fill behind the day, in the selection's shape (a holiday's tint).</summary>
    public Color? BackgroundColor { get; init; }

    /// <summary>The number's font attributes, instead of <see cref="SkUiCalendar.FontAttributes"/>.</summary>
    public FontAttributes? FontAttributes { get; init; }

    /// <summary>Whether the number is struck through (a cancelled day).</summary>
    public bool IsStrikethrough { get; init; }

    /// <summary>Marks in the area under the number (dots, bars, icons, captions), or <c>null</c>.</summary>
    public IReadOnlyList<SkUiCalendarMarker>? Markers { get; init; }
}

/// <summary>
/// A day as <see cref="SkUiCalendar.DayPainter"/> gets it: where it is, its parts and its state, and the colors the calendar
/// would draw it with. <see cref="DrawText"/> draws text through the calendar's text engine (shaped, laid out once and kept).
/// Coordinates are the calendar's, already mirrored right-to-left.
/// </summary>
public readonly struct SkUiCalendarDayContext
{
    private readonly SkUiCalendarModel _model;

    internal SkUiCalendarDayContext(SkUiCalendarModel model, DateTime date, SKRect bounds, SKRect numberBounds, SKRect markerBounds,
        bool isSelected, SkUiCalendarRangePart rangePart, bool isToday, bool isEnabled, bool isAdjacent, bool isFocused, SKColor textColor,
        SkUiCalendarDayStyle? style)
    {
        _model = model;
        Date = date;
        Bounds = bounds;
        NumberBounds = numberBounds;
        MarkerBounds = markerBounds;
        IsSelected = isSelected;
        RangePart = rangePart;
        IsToday = isToday;
        IsEnabled = isEnabled;
        IsAdjacent = isAdjacent;
        IsFocused = isFocused;
        TextColor = textColor;
        Style = style;
    }

    /// <summary>The day.</summary>
    public DateTime Date { get; }

    /// <summary>The whole cell.</summary>
    public SKRect Bounds { get; }

    /// <summary>The part of the cell the number and the look's chrome (selection, today) take: the cell above the marker area.</summary>
    public SKRect NumberBounds { get; }

    /// <summary>The marker area under the number (<see cref="SkUiCalendar.DayMarkerAreaHeight"/>); empty when there is none.</summary>
    public SKRect MarkerBounds { get; }

    /// <summary>The day is selected (single, multiple, or a range's first or last day).</summary>
    public bool IsSelected { get; }

    /// <summary>Which part of a selected range the day is.</summary>
    public SkUiCalendarRangePart RangePart { get; }

    /// <summary>The day is today.</summary>
    public bool IsToday { get; }

    /// <summary>The day can be chosen.</summary>
    public bool IsEnabled { get; }

    /// <summary>The day belongs to the month before or after the page's.</summary>
    public bool IsAdjacent { get; }

    /// <summary>The keyboard's focus is on the day.</summary>
    public bool IsFocused { get; }

    /// <summary>The color the calendar draws the number in (for the day's state).</summary>
    public SKColor TextColor { get; }

    /// <summary>What <see cref="SkUiCalendar.DayStyle"/> returned for the day, or <c>null</c>.</summary>
    public SkUiCalendarDayStyle? Style { get; }

    /// <summary>
    /// Draws <paramref name="text"/> centered (or by <paramref name="alignment"/>) in <paramref name="bounds"/>, on one line,
    /// shrunk a little or shortened when it does not fit. <paramref name="fontSize"/> NaN: the calendar's day font size;
    /// <paramref name="fontFamily"/> <c>null</c>: the calendar's.
    /// </summary>
    public void DrawText(SKCanvas canvas, string text, SKRect bounds, SKColor color, double fontSize = double.NaN,
        FontAttributes attributes = Microsoft.Maui.Controls.FontAttributes.None, string? fontFamily = null, TextAlignment alignment = TextAlignment.Center) =>
        _model?.DrawCustomText(canvas, text, bounds, color, fontSize, attributes, fontFamily, alignment);
}

/// <summary>
/// Draws a calendar day the app's way (<see cref="SkUiCalendar.DayPainter"/>), after the look's chrome (selection, range band,
/// today). Returns <c>true</c> when it drew the day's contents itself: the calendar then draws neither the number nor the
/// markers; <c>false</c>: they are drawn over what the painter drew.
/// </summary>
public delegate bool SkUiCalendarDayPainter(SKCanvas canvas, in SkUiCalendarDayContext day);

/// <summary>What changed in a calendar's selection (<see cref="SkUiCalendar.SelectionChanged"/>).</summary>
public sealed class SkUiCalendarSelectionChangedEventArgs(IReadOnlyList<DateTime> addedDates, IReadOnlyList<DateTime> removedDates,
    SkUiDateRange? oldRange, SkUiDateRange? newRange) : EventArgs
{
    /// <summary>Days that became selected (single and multiple selection).</summary>
    public IReadOnlyList<DateTime> AddedDates { get; } = addedDates;

    /// <summary>Days that are no longer selected (single and multiple selection).</summary>
    public IReadOnlyList<DateTime> RemovedDates { get; } = removedDates;

    /// <summary>The range before the change (range selection).</summary>
    public SkUiDateRange? OldRange { get; } = oldRange;

    /// <summary>The range after the change (range selection).</summary>
    public SkUiDateRange? NewRange { get; } = newRange;
}

/// <summary>
/// A week number the user tapped on a calendar (<see cref="SkUiCalendar.WeekNumberTapped"/>): the week's days and which of
/// them can be chosen, to select the whole week (<c>calendar.SelectedRange = e.Range</c>) or open it elsewhere.
/// </summary>
public sealed class SkUiCalendarWeekNumberTappedEventArgs(DateTime weekStart, DateTime weekEnd, int weekNumber, DateTime? firstEnabledDay,
    DateTime? lastEnabledDay) : EventArgs
{
    /// <summary>The week's first day (its first column).</summary>
    public DateTime WeekStart { get; } = weekStart;

    /// <summary>The week's last day.</summary>
    public DateTime WeekEnd { get; } = weekEnd;

    /// <summary>The number shown (ISO 8601 for Monday weeks under the "first four-day week" rule, else the culture's).</summary>
    public int WeekNumber { get; } = weekNumber;

    /// <summary>The week's first day that can be chosen (the range, blackout dates, <c>DateEnabled</c>), or <c>null</c> when none can.</summary>
    public DateTime? FirstEnabledDay { get; } = firstEnabledDay;

    /// <summary>The week's last day that can be chosen, or <c>null</c> when none can.</summary>
    public DateTime? LastEnabledDay { get; } = lastEnabledDay;

    /// <summary>The week's days as a range, from <see cref="WeekStart"/> to <see cref="WeekEnd"/>.</summary>
    public SkUiDateRange Range => new(WeekStart, WeekEnd);

    /// <summary>The days of the week that can be chosen, from <see cref="FirstEnabledDay"/> to <see cref="LastEnabledDay"/>, or <c>null</c> when none can.</summary>
    public SkUiDateRange? EnabledRange => FirstEnabledDay is { } first && LastEnabledDay is { } last ? new SkUiDateRange(first, last) : null;
}

/// <summary>A day, month, year or decade the user tapped on a calendar (<see cref="SkUiCalendar.DateTapped"/>).</summary>
public sealed class SkUiCalendarDateTappedEventArgs(DateTime date, SkUiCalendarDisplayMode displayMode, bool isEnabled) : EventArgs
{
    /// <summary>The tapped cell's first day (a month's first day on the year page).</summary>
    public DateTime Date { get; } = date;

    /// <summary>The page the cell was on.</summary>
    public SkUiCalendarDisplayMode DisplayMode { get; } = displayMode;

    /// <summary>Whether the cell can be chosen (a disabled day can still be tapped).</summary>
    public bool IsEnabled { get; } = isEnabled;
}
