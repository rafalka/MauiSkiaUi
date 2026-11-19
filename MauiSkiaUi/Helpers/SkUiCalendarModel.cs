using System.Globalization;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>What a calendar's engine asks of the control that holds it (<see cref="SkUiCalendar"/>, <see cref="Core.SkUiCoreCalendar"/>).</summary>
internal interface ISkUiCalendarHost : ISkUiTransitionHost
{
    /// <summary>The calendar is laid out right-to-left.</summary>
    bool CalendarRightToLeft { get; }

    /// <summary>The calendar is enabled.</summary>
    bool CalendarEnabled { get; }

    /// <summary>The calendar's size, for keys and screen readers that move the focus.</summary>
    Size CalendarSize { get; }

    /// <summary>Something drawn changed: re-record (and re-measure with <paramref name="measure"/>).</summary>
    void CalendarInvalidate(bool measure = false);

    /// <summary>The user (or a method) moved the page or changed the display mode: write <c>DisplayDate</c> and <c>DisplayMode</c> back.</summary>
    void CalendarDisplayChanged();

    /// <summary>The user changed the selection: write it back and raise <c>SelectionChanged</c> with <paramref name="args"/>.</summary>
    void CalendarSelectionChanged(SkUiCalendarSelectionChangedEventArgs args);

    /// <summary>The user moved <c>RangeSelectionTarget</c> without changing the selection: write it back.</summary>
    void CalendarRangeTargetChanged();

    /// <summary>The user tapped a cell: raise <c>DateTapped</c>.</summary>
    void CalendarDateTapped(SkUiCalendarDateTappedEventArgs args);

    /// <summary>A subclass's drawing of a header button (<paramref name="isNext"/>: the next one); <c>true</c> when it drew it.</summary>
    bool CalendarDrawNavigationButton(SKCanvas canvas, in SkUiCalendarNavigationButtonPaint button, bool isNext);

    /// <summary>The app's <c>TodayStyle</c>, or <c>null</c> when it set none (the look's then).</summary>
    SkUiCalendarTodayStyle? CalendarTodayStyle { get; }

    /// <summary>The app's <c>SelectionShape</c>, or <c>null</c> when it set none (the look's then).</summary>
    SkUiCalendarSelectionShape? CalendarSelectionShape { get; }

    /// <summary>A subclass's drawing of the header's title; <c>true</c> when it drew it.</summary>
    bool CalendarDrawTitle(SKCanvas canvas, SKRect bounds, string title, SKColor color, bool isPressed);

    /// <summary>The user tapped a week number: raise <c>WeekNumberTapped</c>.</summary>
    void CalendarWeekNumberTapped(SkUiCalendarWeekNumberTappedEventArgs args);
}

/// <summary>The parts of a calendar a point can hit.</summary>
internal enum SkUiCalendarPart
{
    None,
    Title,
    Previous,
    Next,
    Cell,
    WeekNumber
}

/// <summary>What a point hit on a calendar: a part, and for a cell its date and whether it is enabled.</summary>
internal readonly record struct SkUiCalendarHit(SkUiCalendarPart Part, DateTime Date = default, int Index = -1, bool IsEnabled = false);

/// <summary>
/// The state and behavior shared by <see cref="SkUiCalendar"/> and <see cref="Core.SkUiCoreCalendar"/>: the settings the
/// controls copy in, the page shown and its display mode, the selection, the page slide and the zoom between modes (on the
/// look's <see cref="SkUiTransitionKind.CalendarPage"/> transition), measuring, drawing, hit-testing, keys and semantics.
/// The control writes the user's changes back to its properties through <see cref="ISkUiCalendarHost"/>.
/// </summary>
internal sealed class SkUiCalendarModel(ISkUiCalendarHost host)
{
    /// <summary>The share of a page a swipe must pass to settle on the next page.</summary>
    private const float SettleShare = 0.3f;

    /// <summary>A swipe faster than this (DIPs per second, along the axis) settles on the next page however far it went.</summary>
    private const double FlingVelocity = 400;

    /// <summary>How much of a drag past the first or last page shows (a rubber band).</summary>
    private const float EdgeResistance = 0.25f;

    /// <summary>A pinch to below this scale zooms out a display mode, above <see cref="PinchIn"/> zooms in.</summary>
    private const double PinchOut = 0.8;

    private const double PinchIn = 1.25;

    /// <summary>How far the page being left or shown scales during a zoom between display modes.</summary>
    private const float ZoomScale = 0.15f;

    /// <summary>How many day styles the app's callback answered are kept.</summary>
    private const int DayStyleLimit = 512;

    private const int SlotDay = 0;
    private const int SlotDayName = 1;
    private const int SlotTitle = 2;
    private const int SlotWeekNumber = 3;

    // Day numbers and week numbers drawn without a string per frame (the calendar shows Western digits, as DateTime's formats);
    // a culture's FirstDay week rule numbers up to 54 weeks in a leap year starting on the last day of the week.
    private static readonly string[] Numbers = [.. Enumerable.Range(0, 55).Select(number => number.ToString(CultureInfo.InvariantCulture))];

    #region Settings (copied in by the controls)

    public DayOfWeek? FirstDayOfWeek;
    public int VisibleWeeks = SkUiCalendarGrid.MaxWeeks;
    public StackOrientation NavigationOrientation = StackOrientation.Horizontal;
    public bool IsSwipeEnabled = true;
    public bool IsPinchEnabled = true;
    public SkUiCalendarDisplayMode MinimumDisplayMode = SkUiCalendarDisplayMode.Month;
    public SkUiCalendarDisplayMode MaximumDisplayMode = SkUiCalendarDisplayMode.Century;

    public bool IsHeaderVisible = true;
    public SkUiCalendarHeaderLayout HeaderLayout = SkUiCalendarHeaderLayout.TitleFirst;
    public bool AreNavigationButtonsVisible = true;
    public string? TitleFormat;
    public Color? HeaderTextColor;
    public double HeaderFontSize = double.NaN;
    public FontAttributes HeaderFontAttributes = FontAttributes.Bold;
    public Color? NavigationButtonColor;
    public Color? NavigationButtonBackgroundColor;
    public double NavigationButtonSize = -1;
    public double NavigationButtonSpacing = -1;
    public double NavigationButtonCornerRadius = -1;
    public Color? NavigationButtonBorderColor;
    public double NavigationButtonBorderWidth;
    public string? PreviousButtonGlyph;
    public string? NextButtonGlyph;
    public string? NavigationButtonFontFamily;
    public double NavigationButtonGlyphSize = double.NaN;
    public string? HeaderFontFamily;
    private SkUiImageSource? _previousIcon, _nextIcon;
    private SkUiImageSlot? _previousSlot, _nextSlot;

    /// <summary>The previous button's image (instead of the glyph or chevron), loaded through the shared image loader and cache.</summary>
    public SkUiImageSource? PreviousButtonIcon
    {
        get => _previousIcon;
        set => SetIcon(ref _previousIcon, ref _previousSlot, value);
    }

    /// <summary>The next button's image.</summary>
    public SkUiImageSource? NextButtonIcon
    {
        get => _nextIcon;
        set => SetIcon(ref _nextIcon, ref _nextSlot, value);
    }

    private void SetIcon(ref SkUiImageSource? field, ref SkUiImageSlot? slot, SkUiImageSource? value)
    {
        if (Equals(field, value))
            return;
        field = value;
        if (value is null && slot is null)
            return;
        _ = (slot ??= new SkUiImageSlot(host, () => host.CalendarInvalidate())).Load(value, default);
    }

    /// <summary>Whether a button's image is loaded (tests).</summary>
    internal bool HasButtonImage(bool next) => (next ? _nextSlot : _previousSlot)?.Image is not null;

    /// <summary>The loads of the buttons' images (completed when idle; tests).</summary>
    internal Task ButtonIconsLoading => Task.WhenAll(_previousSlot?.LoadingTask ?? Task.CompletedTask, _nextSlot?.LoadingTask ?? Task.CompletedTask);
    public double HeaderHeight = -1;
    public Thickness HeaderPadding = new(8, 0);

    public bool AreDayNamesVisible = true;
    public SkUiCalendarDayNameFormat DayNameFormat = SkUiCalendarDayNameFormat.Shortest;
    public Color? DayNameTextColor;
    public double DayNameFontSize = double.NaN;
    public FontAttributes DayNameFontAttributes = FontAttributes.None;
    public bool IsTodayDayNameHighlighted = true;
    public Color? TodayDayNameTextColor;

    public bool AreAdjacentDaysVisible = true;
    public bool AreWeekNumbersVisible;
    public Color? TextColor;
    public string? FontFamily;
    public double FontSize = double.NaN;
    public FontAttributes FontAttributes = FontAttributes.None;
    public bool FontAutoScalingEnabled = true;
    public double DayRowHeight = -1;

    /// <summary>How today is marked: the app's, else the look's (<see cref="SkUiLook.CalendarTodayStyle"/>).</summary>
    public SkUiCalendarTodayStyle TodayStyle => host.CalendarTodayStyle ?? SkUiLook.Current.CalendarTodayStyle;
    public Color? TodayColor;
    public Color? TodayTextColor;
    public FontAttributes TodayFontAttributes = FontAttributes.Bold;

    /// <summary>The selected days' shape: the app's, else the look's (<see cref="SkUiLook.CalendarSelectionShape"/>).</summary>
    public SkUiCalendarSelectionShape SelectionShape => host.CalendarSelectionShape ?? SkUiLook.Current.CalendarSelectionShape;
    public Color? SelectionColor;
    public Color? SelectedTextColor;
    public Color? RangeColor;
    public Color? RangeTextColor;
    public Color? AdjacentDayTextColor;
    public Color? DisabledDayTextColor;
    public Color? WeekendTextColor;

    public SkUiCalendarSelectionMode SelectionMode = SkUiCalendarSelectionMode.Single;
    public bool AllowsDeselection;
    public DateTime? MinimumDate;
    public DateTime? MaximumDate;
    private SkUiCalendarSystem? _system;

    /// <summary>
    /// The calendar the pages are cut by (1.15): <c>null</c> or a <see cref="GregorianCalendar"/>, the Gregorian one; a supported
    /// other calendar (<see cref="SkUiCalendarSystem.IsSupported"/>), its months and years. The host validates it.
    /// </summary>
    public Calendar? CalendarSystem
    {
        get;
        set
        {
            field = value;
            _system = SkUiCalendarSystem.For(value);
            _display = ClampToRange(_display);
            _focus = _focus is { } focus ? ClampToRange(focus) : null;
        }
    }

    /// <summary>The first day the pages show and a day can be chosen: <see cref="MinimumDate"/>, within the calendar system's range.</summary>
    private DateTime? Min => _system is { } system ? MinimumDate is { } min && min.Date > system.Minimum ? min : system.Minimum : MinimumDate;

    /// <summary>The last day the pages show and a day can be chosen: <see cref="MaximumDate"/>, within the calendar system's range.</summary>
    private DateTime? Max => _system is { } system ? MaximumDate is { } max && max.Date < system.Maximum ? max : system.Maximum : MaximumDate;
    public Func<DateTime, bool>? DateEnabled;
    public SkUiCalendarRangeTarget RangeSelectionTarget = SkUiCalendarRangeTarget.Auto;
    /// <summary>The fewest days a range the user ends may have (0 or 1: any).</summary>
    public int MinimumRangeDays;
    /// <summary>The most days a range the user ends may have (0: any).</summary>
    public int MaximumRangeDays;
    /// <summary>Whether a range the user ends may run over days that cannot be chosen (they are still not chosen themselves).</summary>
    public bool AllowsDisabledDaysInRange = true;
    public double DayMarkerAreaHeight = -1;
    public SkUiCalendarDayMarkerLayout DayMarkerLayout = SkUiCalendarDayMarkerLayout.Row;
    public double DayMarkerLineHeight = -1;
    public SkUiCalendarDayPainter? DayPainter;

    private IList<DateTime>? _blackoutDates;
    private HashSet<DateTime>? _blackout;
    private Func<DateTime, SkUiCalendarDayStyle?>? _dayStyle;
    private readonly Dictionary<DateTime, SkUiCalendarDayStyle?> _dayStyles = [];

    /// <summary>Days that cannot be chosen; read again after <see cref="BlackoutDatesChanged"/>.</summary>
    public IList<DateTime>? BlackoutDates
    {
        get => _blackoutDates;
        set
        {
            _blackoutDates = value;
            _blackout = null;
            _rulesVersion++;
        }
    }

    /// <summary>The app's style per day (read while drawing, remembered until <see cref="RefreshDayStyles"/>).</summary>
    public Func<DateTime, SkUiCalendarDayStyle?>? DayStyle
    {
        get => _dayStyle;
        set
        {
            _dayStyle = value;
            _dayStyles.Clear();
            SweepMarkerImages();
        }
    }

    /// <summary>The blackout list changed (its items): the days are read again.</summary>
    public void BlackoutDatesChanged()
    {
        _blackout = null;
        _rulesVersion++;
    }

    /// <summary>Forgets the day styles the app's callback answered and what was worked out from its day rule: they are asked again when drawn.</summary>
    public void RefreshDayStyles()
    {
        _dayStyles.Clear();
        _rulesVersion++;
        SweepMarkerImages();
    }

    // Image markers (1.15): one slot per distinct source, not per day, marked with the generation it was last drawn in.
    private readonly Dictionary<SkUiImageSource, (SkUiImageSlot Slot, int Generation)> _markerImages = [];
    private int _markerGeneration;

    /// <summary>The slot that draws <paramref name="source"/>, loading it the first time it is asked for.</summary>
    private SkUiImageSlot MarkerImage(SkUiImageSource source)
    {
        if (_markerImages.TryGetValue(source, out var entry))
        {
            if (entry.Generation != _markerGeneration)
                _markerImages[source] = (entry.Slot, _markerGeneration);
            return entry.Slot;
        }
        var slot = new SkUiImageSlot(host, () => host.CalendarInvalidate());
        _markerImages[source] = (slot, _markerGeneration);
        _ = slot.Load(source, default);
        return slot;
    }

    /// <summary>
    /// The day styles are asked again: the images no day drew since the last time are let go (their cache entries go back to the
    /// cache), the others kept so the days that still show them do not wait for a load.
    /// </summary>
    private void SweepMarkerImages()
    {
        if (_markerImages.Count == 0)
            return;
        List<SkUiImageSource>? unused = null;
        foreach (var (source, entry) in _markerImages)
            if (entry.Generation != _markerGeneration)
                (unused ??= []).Add(source);
        if (unused is not null)
            foreach (var source in unused)
            {
                _markerImages[source].Slot.Dispose();
                _markerImages.Remove(source);
            }
        _markerGeneration++;
    }

    /// <summary>The image markers loaded or loading (tests).</summary>
    internal int MarkerImageCount => _markerImages.Count;

    /// <summary>The loads of the image markers (completed when idle; tests).</summary>
    internal Task MarkerImagesLoading => Task.WhenAll(_markerImages.Values.Select(entry => entry.Slot.LoadingTask));

    #endregion

    #region State

    private DateTime _display = DateTime.Today;
    private SkUiCalendarDisplayMode _mode = SkUiCalendarDisplayMode.Month;
    private DateTime? _selectedDate;
    private readonly List<DateTime> _dates = [];
    private readonly HashSet<DateTime> _dateSet = [];
    private IReadOnlyList<DateTime> _datesSnapshot = [];
    private SkUiDateRange? _range;
    // Multiple ranges: in date order, apart from each other, at most one open (the one being chosen).
    private readonly List<SkUiDateRange> _ranges = [];
    private IReadOnlyList<SkUiDateRange> _rangesSnapshot = [];
    private int _rulesVersion;
    // The nearest days around a range's first day that cannot be chosen, found so far (AllowsDisabledDaysInRange false).
    private (DateTime Anchor, DateTime? Minimum, DateTime? Maximum, Func<DateTime, bool>? Rule, int Version) _boundsKey;
    private DateTime? _disabledBefore, _disabledAfter;
    private int _scannedBefore, _scannedAfter;
    private DateTime? _focus;
    private bool _keyboard;

    private SkUiTween? _slide;
    private bool _dragging;
    private float _drag;
    private float _dragFrom; // where the page was when the swipe started (a slide still running)
    private SkUiTween? _zoom;
    private SkUiCalendarDisplayMode _zoomFromMode;
    private DateTime _zoomFromStart;

    private SkUiCalendarHit _pressed;

    private readonly SkUiCalendarTexts _texts = new();
    private string? _title;
    private (DateTime Start, SkUiCalendarDisplayMode Mode, int Weeks, string? Format, CultureInfo Culture) _titleKey;
    private string[]? _dayNames;
    private (DayOfWeek First, SkUiCalendarDayNameFormat Format, CultureInfo Culture, SkUiStrings Strings, int Version) _dayNamesKey;
    private SKPaint? _layerPaint;
    private SKPaint? _markerPaint;

    /// <summary>The date whose page shows (its time dropped).</summary>
    public DateTime DisplayDate => _display;

    /// <summary>What the page shows.</summary>
    public SkUiCalendarDisplayMode DisplayMode => _mode;

    /// <summary>The single selection, or the last date chosen in the other modes.</summary>
    public DateTime? SelectedDate => _selectedDate;

    /// <summary>The multiple selection, in date order (a new list per change).</summary>
    public IReadOnlyList<DateTime> SelectedDates => _datesSnapshot;

    /// <summary>The range selection (multiple ranges: the range changed last).</summary>
    public SkUiDateRange? SelectedRange => _range;

    /// <summary>The ranges of multiple-range selection, in date order (a new list per change).</summary>
    public IReadOnlyList<SkUiDateRange> SelectedRanges => _rangesSnapshot;

    /// <summary>The current culture formatting in the Gregorian calendar the pages are laid out in.</summary>
    private CultureInfo Culture => _system?.Culture(CultureInfo.CurrentCulture) ?? SkUiCalendarGrid.Gregorian(CultureInfo.CurrentCulture);

    /// <summary>The culture week numbers are counted by: the current one in the Gregorian calendar, whatever calendar the pages use.</summary>
    private static CultureInfo WeekCulture => SkUiCalendarGrid.Gregorian(CultureInfo.CurrentCulture);

    /// <summary>The first day of the week in effect.</summary>
    public DayOfWeek EffectiveFirstDayOfWeek => SkUiCalendarGrid.FirstDayOfWeek(FirstDayOfWeek, Culture);

    /// <summary>The first day of the page shown.</summary>
    public DateTime PageStart
    {
        get
        {
            if (_system is null)
                return SkUiCalendarGrid.PageStart(_display, _mode, VisibleWeeks, EffectiveFirstDayOfWeek);
            // A calendar system's date parts cost (and allocate on some calendars): the page start is kept until what it depends on changes.
            var key = (_display, _mode, VisibleWeeks, EffectiveFirstDayOfWeek, _system);
            if (key != _pageStartKey)
            {
                _pageStartKey = key;
                _pageStart = SkUiCalendarGrid.PageStart(_display, _mode, VisibleWeeks, EffectiveFirstDayOfWeek, _system);
            }
            return _pageStart;
        }
    }

    private (DateTime, SkUiCalendarDisplayMode, int, DayOfWeek, SkUiCalendarSystem?) _pageStartKey;
    private DateTime _pageStart;

    // The last steps asked for (the header's buttons and a slide's neighbor ask every frame), kept for a calendar system.
    private readonly (DateTime Start, SkUiCalendarDisplayMode Mode, int Weeks, int Pages, SkUiCalendarSystem? System, DateTime? Target)[] _steps = new
        (DateTime, SkUiCalendarDisplayMode, int, int, SkUiCalendarSystem?, DateTime?)[4];
    private int _nextStep;

    /// <summary><see cref="SkUiCalendarGrid.Step"/> on the calendar shown, kept for a calendar system.</summary>
    private DateTime? StepFrom(DateTime start, int pages)
    {
        if (_system is null)
            return SkUiCalendarGrid.Step(start, _mode, VisibleWeeks, pages);
        foreach (var step in _steps)
            if (step.System == _system && step.Start == start && step.Mode == _mode && step.Weeks == VisibleWeeks && step.Pages == pages)
                return step.Target;
        var target = SkUiCalendarGrid.Step(start, _mode, VisibleWeeks, pages, _system);
        _steps[_nextStep] = (start, _mode, VisibleWeeks, pages, _system, target);
        _nextStep = (_nextStep + 1) % _steps.Length;
        return target;
    }

    // The month boundaries around a page of a calendar system, worked out once per page: which month a cell is in and its day number
    // are then date comparisons (no calendar call per cell, none for days the calendar does not support).
    // A few pages: a slide draws the page and its neighbor in every frame, and a drag back and forth shows the neighbors either side.
    private readonly (DateTime Key, SkUiCalendarSystem? System, DateTime Previous, DateTime Start, DateTime Next, DateTime After)[] _monthsCache =
        new (DateTime, SkUiCalendarSystem?, DateTime, DateTime, DateTime, DateTime)[4];
    private int _monthsNext;
    private (DateTime Key, SkUiCalendarSystem? System, DateTime Previous, DateTime Start, DateTime Next, DateTime After) _months;

    private void MonthsAround(DateTime start)
    {
        var system = _system!;
        if (_months.Key == start && _months.System == system)
            return;
        foreach (var entry in _monthsCache)
            if (entry.Key == start && entry.System == system)
            {
                _months = entry;
                return;
            }
        var first = system.MonthStart(start);
        var previous = system.AddMonths(first, -1) is { } before ? system.MonthStart(before) : DateTime.MinValue;
        var next = system.AddMonths(first, 1) is { } month ? system.MonthStart(month) : system.Maximum.AddDays(1);
        var after = next <= system.Maximum && system.AddMonths(next, 1) is { } later ? system.MonthStart(later) : DateTime.MaxValue.Date;
        _months = (start, system, previous, first, next, after);
        _monthsCache[_monthsNext] = _months;
        _monthsNext = (_monthsNext + 1) % _monthsCache.Length;
    }

    /// <summary>The number a day's cell shows on the page starting at <paramref name="start"/>; -1 for a day the calendar system does not support.</summary>
    private int DayNumber(DateTime start, DateTime day)
    {
        if (_system is not { } system)
            return day.Day;
        if (day < system.Minimum || day > system.Maximum)
            return -1;
        MonthsAround(start);
        var (_, _, previous, first, next, after) = _months;
        var from = day >= after ? after : day >= next ? next : day >= first ? first : day >= previous ? previous : DateTime.MinValue;
        return from == DateTime.MinValue ? system.Day(day) : (day - from).Days + 1;
    }

    /// <summary>The header's title for the page shown (kept until the page, mode, format or culture changes).</summary>
    public string Title
    {
        get
        {
            (DateTime Start, SkUiCalendarDisplayMode Mode, int Weeks, string? Format, CultureInfo Culture) key =
                (PageStart, _mode, VisibleWeeks, TitleFormat, Culture);
            if (_title is null || key != _titleKey)
            {
                _titleKey = key;
                _title = SkUiCalendarGrid.Title(key.Start, key.Mode, key.Weeks, key.Format, key.Culture, _system);
            }
            return _title;
        }
    }

    /// <summary>Whether a page slides or the display mode zooms now (tests).</summary>
    public bool IsAnimating => _dragging || _slide is { IsRunning: true } || _zoom is { IsRunning: true };

    /// <summary>The page offset drawn now, in pages (tests).</summary>
    public float SlideOffset => _dragging ? _drag : _slide is { IsRunning: true } slide ? slide.Value : 0;

    /// <summary>The day the keyboard and screen readers act on (tests).</summary>
    public DateTime? FocusDate => _focus;

    /// <summary>Text layouts kept (tests).</summary>
    internal int CachedTextCount => _texts.Count;

    /// <summary>The texts laid out and kept (tests).</summary>
    internal IEnumerable<string> CachedTexts => _texts.Texts;

    #endregion

    #region Rules

    /// <summary><paramref name="date"/>'s day within <see cref="MinimumDate"/> and <see cref="MaximumDate"/>.</summary>
    public DateTime ClampToRange(DateTime date)
    {
        var day = date.Date;
        if (Min is { } min && day < min.Date)
            day = min.Date;
        if (Max is { } max && day > max.Date && (Min is not { } low || max.Date >= low.Date))
            day = max.Date;
        return day;
    }

    /// <summary><paramref name="mode"/> within <see cref="MinimumDisplayMode"/> and <see cref="MaximumDisplayMode"/> (a maximum below the minimum counts as the minimum).</summary>
    public SkUiCalendarDisplayMode ClampMode(SkUiCalendarDisplayMode mode)
    {
        var max = MaximumDisplayMode < MinimumDisplayMode ? MinimumDisplayMode : MaximumDisplayMode;
        return mode < MinimumDisplayMode ? MinimumDisplayMode : mode > max ? max : mode;
    }

    private bool IsInRange(DateTime day) =>
        (Min is not { } min || day >= min.Date) && (Max is not { } max || day <= max.Date);

    private bool IsBlackout(DateTime day)
    {
        if (_blackoutDates is not { Count: > 0 } list)
            return false;
        if (_blackout is null)
        {
            _blackout = [];
            foreach (var date in list)
                _blackout.Add(date.Date);
        }
        return _blackout.Contains(day);
    }

    /// <summary>Whether <paramref name="day"/> can be chosen: in the range, not a blackout date, and allowed by <see cref="DateEnabled"/>.</summary>
    public bool IsDayEnabled(DateTime day) => IsInRange(day) && !IsBlackout(day) && (DateEnabled?.Invoke(day) ?? true);

    /// <summary>
    /// Whether the user can choose <paramref name="day"/> now: <see cref="IsDayEnabled"/>, and while a range waits for its other
    /// end, a day that ends it within <see cref="MinimumRangeDays"/> and <see cref="MaximumRangeDays"/> (and, without
    /// <see cref="AllowsDisabledDaysInRange"/>, before the nearest day that cannot be chosen).
    /// </summary>
    public bool IsChoosable(DateTime day) => IsDayEnabled(day) && RangeAllows(day);

    /// <summary>The first day of the range a tap would end now; <c>null</c> when a tap starts one (or the mode has no ranges).</summary>
    private DateTime? RangeAnchor
    {
        get
        {
            if (SelectionMode == SkUiCalendarSelectionMode.MultipleRanges)
            {
                foreach (var range in _ranges)
                    if (!range.IsComplete)
                        return range.Start;
                return null;
            }
            if (SelectionMode != SkUiCalendarSelectionMode.Range)
                return null;
            return RangeSelectionTarget switch
            {
                SkUiCalendarRangeTarget.Start => null,
                SkUiCalendarRangeTarget.End => _range?.Start,
                _ => _range is { IsComplete: false } open ? open.Start : null
            };
        }
    }

    private bool HasRangeRules => MinimumRangeDays > 1 || MaximumRangeDays > 0 || !AllowsDisabledDaysInRange;

    private bool RangeAllows(DateTime day) => !HasRangeRules || RangeAnchor is not { } anchor || day == anchor || RangeFits(anchor, day);

    /// <summary>Whether a range from <paramref name="anchor"/> to <paramref name="day"/> (either way) keeps the range rules.</summary>
    private bool RangeFits(DateTime anchor, DateTime day)
    {
        var days = Math.Abs((day - anchor).Days) + 1;
        if (MinimumRangeDays > 1 && days < MinimumRangeDays || MaximumRangeDays > 0 && days > MaximumRangeDays)
            return false;
        return AllowsDisabledDaysInRange || NoDisabledDayBetween(anchor, day);
    }

    /// <summary>
    /// Whether every day from <paramref name="anchor"/> to <paramref name="day"/> can be chosen. The nearest days that cannot,
    /// either side of the anchor, are found once and kept (until the anchor or a rule changes), scanning only as far as asked.
    /// </summary>
    private bool NoDisabledDayBetween(DateTime anchor, DateTime day)
    {
        var key = (anchor, Min, Max, DateEnabled, _rulesVersion);
        if (key != _boundsKey)
        {
            _boundsKey = key;
            _disabledBefore = _disabledAfter = null;
            _scannedBefore = _scannedAfter = 0;
        }
        var distance = Math.Abs((day - anchor).Days);
        var forward = day > anchor;
        ref var found = ref forward ? ref _disabledAfter : ref _disabledBefore;
        ref var scanned = ref forward ? ref _scannedAfter : ref _scannedBefore;
        while (found is null && scanned < distance)
        {
            scanned++;
            if (SkUiCalendarGrid.AddDays(anchor, forward ? scanned : -scanned) is not { } next)
            {
                scanned = int.MaxValue; // the end of the calendar
                break;
            }
            if (!IsDayEnabled(next))
                found = next;
        }
        return found is not { } stop || (forward ? day < stop : day > stop);
    }

    /// <summary>Whether a month / year / decade cell can be chosen or opened: part of its span is in the range.</summary>
    private bool IsPeriodEnabled(DateTime cell, SkUiCalendarDisplayMode mode) =>
        (Max is not { } max || cell <= max.Date) && (Min is not { } min || SkUiCalendarGrid.PeriodEnd(cell, mode, _system) >= min.Date);

    private SkUiCalendarDayStyle? StyleOf(DateTime day)
    {
        if (_dayStyle is not { } callback)
            return null;
        if (_dayStyles.TryGetValue(day, out var style))
            return style;
        if (_dayStyles.Count >= DayStyleLimit)
            _dayStyles.Clear();
        style = callback(day);
        _dayStyles[day] = style;
        return style;
    }

    #endregion

    #region Display date and mode

    /// <summary>Shows the page of <paramref name="value"/> (kept in the range) without a slide; returns whether the date changed.</summary>
    public bool SetDisplayDate(DateTime value)
    {
        var day = ClampToRange(value);
        if (day == _display)
            return false;
        var page = PageStart;
        _display = day;
        if (PageStart != page)
            StopSlide();
        return true;
    }

    /// <summary>Shows <paramref name="value"/> (kept within the mode limits) without a zoom; returns whether the mode changed.</summary>
    public bool SetDisplayMode(SkUiCalendarDisplayMode value)
    {
        var mode = ClampMode(value);
        if (mode == _mode)
            return false;
        _mode = mode;
        StopSlide();
        _zoom?.Jump(1);
        return true;
    }

    /// <summary>The display mode limits or the weeks changed: the mode and date are kept within them. Returns whether the mode changed.</summary>
    public bool Revalidate()
    {
        var changed = SetDisplayMode(_mode);
        SetDisplayDate(_display);
        return changed;
    }

    /// <summary>Whether the page can move <paramref name="pages"/> (±1) without leaving the range.</summary>
    public bool CanStep(int pages)
    {
        var start = PageStart;
        if (StepFrom(start, pages) is not { } target)
            return false;
        if (pages >= 0)
            return Max is not { } max || target <= max.Date;
        if (Min is not { } min)
            return true;
        // The header asks every frame; a calendar system's page end is kept for the page and the limit.
        if (_system is not null && _canStepBack.Start == start && _canStepBack.Mode == _mode && _canStepBack.Weeks == VisibleWeeks
            && _canStepBack.System == _system && _canStepBack.Min == min)
            return _canStepBack.Result;
        var result = SkUiCalendarGrid.PageEnd(target, _mode, VisibleWeeks, _system) >= min.Date;
        if (_system is not null)
            _canStepBack = (start, _mode, VisibleWeeks, _system, min, result);
        return result;
    }

    private (DateTime Start, SkUiCalendarDisplayMode Mode, int Weeks, SkUiCalendarSystem? System, DateTime Min, bool Result) _canStepBack;

    /// <summary>Moves the page by <paramref name="pages"/> (±1), sliding when <paramref name="animated"/>; returns whether it moved.</summary>
    public bool Step(int pages, bool animated)
    {
        if (!CanStep(pages) || StepFrom(PageStart, pages) is not { } target)
            return false;
        _display = ClampToRange(target);
        // The focus follows to the same place on the new page when it was on the old one.
        if (_focus is { } focus && _keyboard)
            _focus = ClampToRange(MovePages(focus, pages) ?? focus);
        if (animated)
            StartSlide(VisualPages(pages), from: null);
        else
            StopSlide();
        host.CalendarDisplayChanged();
        host.CalendarInvalidate();
        return true;
    }

    /// <summary>Shows <paramref name="date"/>'s page in the month view (or the lowest mode), sliding when it is another page and <paramref name="animated"/>.</summary>
    public void GoToDate(DateTime date, bool animated)
    {
        var day = ClampToRange(date);
        var before = PageStart;
        var modeChanged = SetDisplayMode(MinimumDisplayMode);
        _display = day;
        var after = PageStart;
        if (animated && !modeChanged && after != before)
            StartSlide(VisualPages(after > before ? 1 : -1), from: null);
        else
            StopSlide();
        host.CalendarDisplayChanged();
        host.CalendarInvalidate();
    }

    /// <summary>A tap on the title: the next mode up, or back to the lowest from the highest.</summary>
    public void StepUpMode()
    {
        var max = ClampMode(MaximumDisplayMode);
        if (_mode < max)
            ZoomTo(_mode + 1, _display);
        else if (max > MinimumDisplayMode)
            ZoomTo(MinimumDisplayMode, _display);
    }

    /// <summary>The next mode down, showing <paramref name="date"/>'s page.</summary>
    public void StepDownMode(DateTime date)
    {
        if (_mode > MinimumDisplayMode)
            ZoomTo(_mode - 1, ClampToRange(date));
    }

    private void ZoomTo(SkUiCalendarDisplayMode mode, DateTime display)
    {
        mode = ClampMode(mode);
        if (mode == _mode)
            return;
        _zoomFromMode = _mode;
        _zoomFromStart = PageStart;
        StopSlide();
        _mode = mode;
        _display = display;
        if (_keyboard && _focus is { } focus)
            _focus = PageContains(focus) ? focus : null;
        var transition = host.TransitionLook.GetTransition(SkUiTransitionKind.CalendarPage);
        _zoom ??= new SkUiTween(host);
        _zoom.Jump(0);
        if (transition.IsNone)
            _zoom.Jump(1);
        else
            _zoom.AnimateTo(1, transition);
        host.CalendarDisplayChanged();
        host.CalendarInvalidate();
    }

    /// <summary>A pinch ended at <paramref name="totalScale"/> around <paramref name="origin"/>: zoom out a mode, or in to the cell under it.</summary>
    public void Pinch(double totalScale, Point origin, float width, float height)
    {
        if (!IsPinchEnabled || IsAnimating)
            return;
        if (totalScale < PinchOut)
            StepUpModeOnly();
        else if (totalScale > PinchIn && _mode > MinimumDisplayMode)
            StepDownMode(HitTest(origin, width, height) is { Part: SkUiCalendarPart.Cell } hit ? hit.Date : _display);
    }

    private void StepUpModeOnly()
    {
        if (_mode < ClampMode(MaximumDisplayMode))
            ZoomTo(_mode + 1, _display);
    }

    /// <summary>The page offset in visual pages for a move of <paramref name="pages"/>: the next page arrives from the right (left right-to-left) or from below.</summary>
    private int VisualPages(int pages) => NavigationOrientation == StackOrientation.Horizontal && host.CalendarRightToLeft ? -pages : pages;

    private void StartSlide(int visualPages, float? from)
    {
        var transition = host.TransitionLook.GetTransition(SkUiTransitionKind.CalendarPage);
        _slide ??= new SkUiTween(host);
        _slide.Jump(from ?? visualPages);
        if (transition.IsNone)
            _slide.Jump(0);
        else
            _slide.AnimateTo(0, transition);
    }

    private void StopSlide()
    {
        _dragging = false;
        _drag = 0;
        _slide?.Jump(0);
    }

    #endregion

    #region Selection

    private static readonly IReadOnlyList<DateTime> NoDates = [];

    /// <summary>Sets the single selection (the app's); returns the change, or <c>null</c> when nothing changed.</summary>
    public SkUiCalendarSelectionChangedEventArgs? SetSelectedDate(DateTime? value)
    {
        var day = value?.Date;
        if (day == _selectedDate)
            return null;
        var old = _selectedDate;
        _selectedDate = day;
        return SelectionMode == SkUiCalendarSelectionMode.Single ? Change(day, old) : null;
    }

    /// <summary>Sets the multiple selection (the app's); returns the change, or <c>null</c> when nothing changed.</summary>
    public SkUiCalendarSelectionChangedEventArgs? SetSelectedDates(IEnumerable<DateTime>? value)
    {
        var next = new SortedSet<DateTime>();
        if (value is not null)
            foreach (var date in value)
                next.Add(date.Date);
        if (next.SetEquals(_dateSet))
            return null;
        var added = next.Where(date => !_dateSet.Contains(date)).ToArray();
        var removed = _dates.Where(date => !next.Contains(date)).ToArray();
        _dates.Clear();
        _dates.AddRange(next);
        _dateSet.Clear();
        _dateSet.UnionWith(next);
        _datesSnapshot = _dates.ToArray();
        return new SkUiCalendarSelectionChangedEventArgs(added, removed, _range, _range);
    }

    /// <summary>Sets the range selection (the app's); returns the change, or <c>null</c> when nothing changed.</summary>
    public SkUiCalendarSelectionChangedEventArgs? SetSelectedRange(SkUiDateRange? value)
    {
        if (Nullable.Equals(value, _range))
            return null;
        var old = _range;
        _range = value;
        return new SkUiCalendarSelectionChangedEventArgs(NoDates, NoDates, old, value);
    }

    /// <summary>
    /// Sets the ranges of multiple-range selection (the app's): in date order, ranges that overlap or touch joined, one open range
    /// kept (the last); returns the change, or <c>null</c> when nothing changed.
    /// </summary>
    public SkUiCalendarSelectionChangedEventArgs? SetSelectedRanges(IEnumerable<SkUiDateRange>? value)
    {
        var next = Normalize(value);
        if (next.SequenceEqual(_ranges))
            return null;
        var old = _range;
        _ranges.Clear();
        _ranges.AddRange(next);
        _rangesSnapshot = _ranges.ToArray();
        if (SelectionMode == SkUiCalendarSelectionMode.MultipleRanges)
            _range = _ranges.Count > 0 ? _ranges[^1] : null;
        return new SkUiCalendarSelectionChangedEventArgs(NoDates, NoDates, old, _range);
    }

    /// <summary>Ranges in date order, those that overlap or touch joined; an open range is kept only when it is apart from the others (the last such).</summary>
    private static List<SkUiDateRange> Normalize(IEnumerable<SkUiDateRange>? ranges)
    {
        var result = new List<SkUiDateRange>();
        if (ranges is null)
            return result;
        SkUiDateRange? open = null;
        foreach (var range in ranges)
            if (range.IsComplete)
                result.Add(range);
            else
                open = range;
        Join(result);
        if (open is { } pending && !result.Any(range => range.Start <= pending.Start && pending.Start <= range.LastDay))
        {
            result.Add(pending);
            result.Sort((a, b) => a.Start.CompareTo(b.Start));
        }
        return result;
    }

    /// <summary>Sorts complete <paramref name="ranges"/> and joins those that overlap or touch.</summary>
    private static void Join(List<SkUiDateRange> ranges)
    {
        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        // Forward, against the joined range so far: a long range takes in every later one it reaches, not only its neighbor.
        var kept = 0;
        for (var index = 1; index < ranges.Count; index++)
        {
            var (current, next) = (ranges[kept], ranges[index]);
            if (SkUiCalendarGrid.AddDays(current.LastDay, 1) is { } after && next.Start > after)
                ranges[++kept] = next;
            else if (next.LastDay > current.LastDay)
                ranges[kept] = new SkUiDateRange(current.Start, next.LastDay);
        }
        if (ranges.Count > 0)
            ranges.RemoveRange(kept + 1, ranges.Count - kept - 1);
    }

    /// <summary>Clears every selection; returns what was cleared, or <c>null</c>.</summary>
    public SkUiCalendarSelectionChangedEventArgs? Clear()
    {
        var removed = new List<DateTime>(_dates);
        if (_selectedDate is { } date && !_dateSet.Contains(date))
            removed.Add(date);
        var old = _range;
        if (removed.Count == 0 && old is null && _ranges.Count == 0)
            return null;
        _selectedDate = null;
        _dates.Clear();
        _dateSet.Clear();
        _datesSnapshot = NoDates;
        _range = null;
        _ranges.Clear();
        _rangesSnapshot = [];
        return new SkUiCalendarSelectionChangedEventArgs(NoDates, removed, old, null);
    }

    private SkUiCalendarSelectionChangedEventArgs Change(DateTime? added, DateTime? removed) => new(
        added is { } a ? [a] : NoDates, removed is { } r ? [r] : NoDates, _range, _range);

    /// <summary>The user chose <paramref name="day"/>: the selection changes by the mode, and the control writes it back.</summary>
    public void Choose(DateTime day)
    {
        SkUiCalendarSelectionChangedEventArgs? args = null;
        switch (SelectionMode)
        {
            case SkUiCalendarSelectionMode.Single:
                var old = _selectedDate;
                if (old == day)
                {
                    if (!AllowsDeselection)
                        return;
                    _selectedDate = null;
                    args = Change(null, old);
                }
                else
                {
                    _selectedDate = day;
                    args = Change(day, old);
                }
                break;
            case SkUiCalendarSelectionMode.Multiple:
                if (_dateSet.Remove(day))
                {
                    _dates.Remove(day);
                    if (_selectedDate == day)
                        _selectedDate = _dates.Count > 0 ? _dates[^1] : null;
                    args = Change(null, day);
                }
                else
                {
                    _dateSet.Add(day);
                    var index = _dates.BinarySearch(day);
                    _dates.Insert(index < 0 ? ~index : index, day);
                    _selectedDate = day;
                    args = Change(day, null);
                }
                _datesSnapshot = _dates.ToArray();
                break;
            case SkUiCalendarSelectionMode.Range:
                var before = _range;
                var target = RangeSelectionTarget;
                _range = NextRange(_range, day);
                _selectedDate = _range?.Start;
                if (Nullable.Equals(before, _range))
                {
                    // The first day tapped again while it is the target: the range stays, the target still moves to the end.
                    if (target != RangeSelectionTarget)
                        host.CalendarRangeTargetChanged();
                    return;
                }
                args = new SkUiCalendarSelectionChangedEventArgs(NoDates, NoDates, before, _range);
                break;
            case SkUiCalendarSelectionMode.MultipleRanges:
                var last = _range;
                if (!ChooseInRanges(day))
                    return;
                _rangesSnapshot = _ranges.ToArray();
                _selectedDate = _range?.Start;
                args = new SkUiCalendarSelectionChangedEventArgs(NoDates, NoDates, last, _range);
                break;
            default:
                return;
        }
        host.CalendarInvalidate();
        host.CalendarSelectionChanged(args);
    }

    /// <summary>
    /// A tap on <paramref name="day"/> in multiple-range selection: it ends the open range (joining the ranges it reaches), removes
    /// the chosen range it is in, or starts a range. Returns whether the ranges changed.
    /// </summary>
    private bool ChooseInRanges(DateTime day)
    {
        var openIndex = _ranges.FindIndex(range => !range.IsComplete);
        if (openIndex >= 0)
        {
            var start = _ranges[openIndex].Start;
            // Too short a range from its own first day: the range stays open.
            if (day == start && MinimumRangeDays > 1)
                return false;
            _ranges.RemoveAt(openIndex);
            _ranges.Add(new SkUiDateRange(start, day));
            Join(_ranges);
            _range = _ranges.First(range => range.Contains(start));
            return true;
        }
        var inside = _ranges.FindIndex(range => range.Contains(day));
        if (inside >= 0)
        {
            _ranges.RemoveAt(inside);
            _range = null;
            return true;
        }
        var opened = new SkUiDateRange(day);
        var index = _ranges.FindIndex(range => range.Start > day);
        _ranges.Insert(index < 0 ? _ranges.Count : index, opened);
        _range = opened;
        return true;
    }

    /// <summary>
    /// The range after a tap on <paramref name="day"/>. Auto: an open range ends, else a new one starts. Start: the first day
    /// moves there (a last day before it is cleared) and the target moves to the end. End: the last day moves there; a day
    /// before the first becomes the first and the old first the last.
    /// </summary>
    private SkUiDateRange? NextRange(SkUiDateRange? range, DateTime day)
    {
        // Too short a range from its own first day: the range stays as it is.
        if (MinimumRangeDays > 1 && RangeAnchor == day)
            return range;
        switch (RangeSelectionTarget)
        {
            case SkUiCalendarRangeTarget.Start:
                RangeSelectionTarget = SkUiCalendarRangeTarget.End;
                // The last day stays when the range still keeps the rules from the new first day.
                return range is { End: { } end } && end >= day && (!HasRangeRules || RangeFits(day, end)) ? new SkUiDateRange(day, end) : new SkUiDateRange(day);
            case SkUiCalendarRangeTarget.End:
                if (range is not { } current)
                    return new SkUiDateRange(day);
                return day >= current.Start ? new SkUiDateRange(current.Start, day) : new SkUiDateRange(day, current.Start);
            default:
                return range is { IsComplete: false } open ? new SkUiDateRange(open.Start, day) : new SkUiDateRange(day);
        }
    }

    private bool IsSelectedDay(DateTime day) => SelectionMode switch
    {
        SkUiCalendarSelectionMode.Single => _selectedDate == day,
        SkUiCalendarSelectionMode.Multiple => _dateSet.Contains(day),
        SkUiCalendarSelectionMode.Range or SkUiCalendarSelectionMode.MultipleRanges => RangeOf(day) is { } range && (day == range.Start || day == range.LastDay),
        _ => false
    };

    /// <summary>The chosen range <paramref name="day"/> is in (range and multiple-range selection), or <c>null</c>.</summary>
    private SkUiDateRange? RangeOf(DateTime day)
    {
        if (SelectionMode == SkUiCalendarSelectionMode.Range)
            return _range is { } range && range.Contains(day) ? range : null;
        if (SelectionMode != SkUiCalendarSelectionMode.MultipleRanges)
            return null;
        foreach (var range in _ranges)
        {
            if (range.Start > day)
                break;
            if (range.Contains(day))
                return range;
        }
        return null;
    }

    /// <summary>Whether a month / year / decade cell holds the selection (shown when it is the lowest mode, where cells are chosen).</summary>
    private bool IsSelectedPeriod(DateTime cell, SkUiCalendarDisplayMode mode)
    {
        if (mode != MinimumDisplayMode)
            return false;
        var end = SkUiCalendarGrid.PeriodEnd(cell, mode, _system);
        return SelectionMode switch
        {
            SkUiCalendarSelectionMode.Single => _selectedDate is { } date && date >= cell && date <= end,
            SkUiCalendarSelectionMode.Multiple => AnySelectedBetween(cell, end),
            SkUiCalendarSelectionMode.Range => _range is { } range && EndsIn(range, cell, end),
            SkUiCalendarSelectionMode.MultipleRanges => _ranges.Any(range => EndsIn(range, cell, end)),
            _ => false
        };
    }

    private static bool EndsIn(SkUiDateRange range, DateTime first, DateTime last) =>
        range.Start >= first && range.Start <= last || range.LastDay >= first && range.LastDay <= last;

    private bool AnySelectedBetween(DateTime first, DateTime last)
    {
        var index = _dates.BinarySearch(first);
        index = index < 0 ? ~index : index;
        return index < _dates.Count && _dates[index] <= last;
    }

    #endregion

    #region Layout

    /// <summary>Where the parts of a calendar of a size go (left-to-right; <see cref="CellRect"/> and the header mirror).</summary>
    // EventNumber: with DayMarkerLayout Lines, the number's height at the top of a day (the markers take the rest), else 0.
    private readonly record struct Frame(float Width, float Height, float HeaderHeight, float DayNamesHeight, float WeekColumn, float MarkerArea, bool RightToLeft,
        float EventNumber = 0)
    {
        public float GridTop(bool month) => HeaderHeight + (month ? DayNamesHeight : 0);
        public SKRect GridArea => new(0, HeaderHeight, Width, Height);
    }

    private Frame Layout(float width, float height, SkUiLook look)
    {
        var header = IsHeaderVisible ? (float)ResolvedHeaderHeight(look) : 0;
        var dayNames = AreDayNamesVisible ? (float)look.CalendarDayNamesHeight : 0;
        var weeks = AreWeekNumbersVisible ? (float)look.CalendarWeekNumberWidth : 0;
        return new Frame(width, height, header, dayNames, weeks, (float)MarkerArea(look), host.CalendarRightToLeft,
            DayMarkerLayout == SkUiCalendarDayMarkerLayout.Lines ? (float)look.CalendarEventNumberHeight : 0);
    }

    /// <summary>The marker area's height under each day: the set one, else the look's when the app styles or paints days, else none.</summary>
    private double MarkerArea(SkUiLook look) => DayMarkerAreaHeight >= 0 ? DayMarkerAreaHeight
        : DayMarkerLayout == SkUiCalendarDayMarkerLayout.Lines ? LinesSizedByItself * MarkerLineHeight(look)
        : _dayStyle is not null || DayPainter is not null ? look.CalendarMarkerAreaHeight : 0;

    /// <summary>The event lines a day holds when a calendar with <see cref="SkUiCalendarDayMarkerLayout.Lines"/> sizes itself.</summary>
    private const int LinesSizedByItself = 3;

    /// <summary>An event line's height: the set one, else the look's.</summary>
    private double MarkerLineHeight(SkUiLook look) => DayMarkerLineHeight > 0 ? DayMarkerLineHeight : look.CalendarMarkerLineHeight;

    /// <summary>The size of a calendar: the width given (else seven touch-sized columns), the header, day names and weeks.</summary>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var look = SkUiLook.Current;
        var row = DayRowHeight > 0 ? DayRowHeight : look.CalendarRowHeight;
        var weekColumn = AreWeekNumbersVisible ? look.CalendarWeekNumberWidth : 0;
        var width = double.IsFinite(widthConstraint) ? widthConstraint : 7 * row + weekColumn;
        // Event lines: the number's own height and the lines under it, or the row height set for the whole day.
        var day = DayMarkerLayout == SkUiCalendarDayMarkerLayout.Lines
            ? DayRowHeight > 0 ? DayRowHeight : look.CalendarEventNumberHeight + MarkerArea(look)
            : row + MarkerArea(look);
        var height = (IsHeaderVisible ? ResolvedHeaderHeight(look) : 0) + (AreDayNamesVisible ? look.CalendarDayNamesHeight : 0)
            + Math.Clamp(VisibleWeeks, 1, 6) * day;
        return new Size(width, height);
    }

    /// <summary>A cell's rectangle on a page of <paramref name="month"/> days (else 4 × 3 periods), mirrored right-to-left.</summary>
    private SKRect CellRect(in Frame frame, bool month, int row, int column)
    {
        var rows = month ? Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks) : SkUiCalendarGrid.PeriodRowCount(_mode, _system);
        var columns = month ? 7 : SkUiCalendarGrid.PeriodColumns;
        var left = month && !frame.RightToLeft ? frame.WeekColumn : 0;
        var right = frame.Width - (month && frame.RightToLeft ? frame.WeekColumn : 0);
        var top = frame.GridTop(month);
        var width = Math.Max(0, right - left) / columns;
        var height = Math.Max(0, frame.Height - top) / rows;
        var x = frame.RightToLeft ? right - (column + 1) * width : left + column * width;
        return SKRect.Create(x, top + row * height, width, height);
    }

    private SKRect WeekNumberRect(in Frame frame, int row)
    {
        var cell = CellRect(frame, month: true, row, 0);
        var x = frame.RightToLeft ? frame.Width - frame.WeekColumn : 0;
        return SKRect.Create(x, cell.Top, frame.WeekColumn, cell.Height);
    }

    /// <summary>The header's height: the set one, else the look's.</summary>
    private double ResolvedHeaderHeight(SkUiLook look) => HeaderHeight >= 0 ? HeaderHeight : look.CalendarHeaderHeight;

    private (SKRect Title, SKRect Previous, SKRect Next) HeaderRects(in Frame frame, SkUiLook look)
    {
        if (frame.HeaderHeight <= 0)
            return default;
        var padding = HeaderPadding;
        var (left, right) = ((float)padding.Left, frame.Width - (float)padding.Right);
        var (top, bottom) = ((float)padding.Top, Math.Max((float)padding.Top, frame.HeaderHeight - (float)padding.Bottom));
        var gap = NavigationButtonSpacing >= 0 ? (float)NavigationButtonSpacing : 4;
        var size = NavigationButtonSize >= 0 ? NavigationButtonSize : look.CalendarNavigationButtonSize;
        var button = AreNavigationButtonsVisible ? (float)Math.Min(size, bottom - top) : 0;
        var buttonTop = top + (bottom - top - button) / 2;
        SKRect title, previous = SKRect.Empty, next = SKRect.Empty;
        if (button <= 0)
            title = new SKRect(left, top, right, bottom);
        else if (HeaderLayout == SkUiCalendarHeaderLayout.Centered)
        {
            previous = SKRect.Create(left, buttonTop, button, button);
            next = SKRect.Create(right - button, buttonTop, button, button);
            title = new SKRect(previous.Right + gap, top, next.Left - gap, bottom);
        }
        else
        {
            next = SKRect.Create(right - button, buttonTop, button, button);
            previous = SKRect.Create(next.Left - gap - button, buttonTop, button, button);
            title = new SKRect(left, top, previous.Left - Math.Max(gap, 4), bottom);
        }
        return frame.RightToLeft ? (Mirror(title, frame.Width), Mirror(previous, frame.Width), Mirror(next, frame.Width)) : (title, previous, next);
    }

    private static SKRect Mirror(SKRect rect, float width) => rect.IsEmpty ? rect : new SKRect(width - rect.Right, rect.Top, width - rect.Left, rect.Bottom);

    /// <summary>The part of a <paramref name="width"/> × <paramref name="height"/> calendar at <paramref name="point"/>.</summary>
    public SkUiCalendarHit HitTest(Point point, float width, float height)
    {
        var look = SkUiLook.Current;
        var frame = Layout(width, height, look);
        var (x, y) = ((float)point.X, (float)point.Y);
        if (y < frame.HeaderHeight)
        {
            var (title, previous, next) = HeaderRects(frame, look);
            if (previous.Contains(x, y))
                return new SkUiCalendarHit(SkUiCalendarPart.Previous, IsEnabled: CanStep(-1));
            if (next.Contains(x, y))
                return new SkUiCalendarHit(SkUiCalendarPart.Next, IsEnabled: CanStep(1));
            return title.Contains(x, y) ? new SkUiCalendarHit(SkUiCalendarPart.Title, IsEnabled: CanChangeMode) : default;
        }
        var month = _mode == SkUiCalendarDisplayMode.Month;
        var start = PageStart;
        if (month && frame.WeekColumn > 0)
        {
            var first = SkUiCalendarGrid.GridStart(start, VisibleWeeks, EffectiveFirstDayOfWeek);
            for (var row = 0; row < Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks); row++)
                if (WeekNumberRect(frame, row).Contains(x, y))
                    return SkUiCalendarGrid.AddDays(first, row * 7) is { } weekStart
                        ? new SkUiCalendarHit(SkUiCalendarPart.WeekNumber, weekStart, row, true)
                        : default;
        }
        var columns = month ? 7 : SkUiCalendarGrid.PeriodColumns;
        var rows = month ? Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks) : SkUiCalendarGrid.PeriodRowCount(_mode, _system);
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (!CellRect(frame, month, row, column).Contains(x, y))
                    continue;
                var index = row * columns + column;
                return month ? DayHit(start, index) : PeriodHit(start, index);
            }
        return default;
    }

    private bool CanChangeMode => ClampMode(MaximumDisplayMode) > MinimumDisplayMode;

    private SkUiCalendarHit DayHit(DateTime start, int index)
    {
        var first = SkUiCalendarGrid.GridStart(start, VisibleWeeks, EffectiveFirstDayOfWeek);
        if (SkUiCalendarGrid.AddDays(first, index) is not { } day)
            return default;
        if (IsAdjacent(start, day) && !AreAdjacentDaysVisible)
            return default;
        return new SkUiCalendarHit(SkUiCalendarPart.Cell, day, index, IsChoosable(day));
    }

    private SkUiCalendarHit PeriodHit(DateTime start, int index)
    {
        if (SkUiCalendarGrid.PeriodCell(start, _mode, index, _system) is not { } cell)
            return default;
        if (SkUiCalendarGrid.IsAdjacentPeriodCell(_mode, index) && !AreAdjacentDaysVisible)
            return default;
        return new SkUiCalendarHit(SkUiCalendarPart.Cell, cell, index, IsPeriodEnabled(cell, _mode));
    }

    /// <summary>Whether <paramref name="day"/> is a neighbor month's day on the month page starting at <paramref name="start"/> (pages of weeks have none).</summary>
    private bool IsAdjacent(DateTime start, DateTime day)
    {
        if (VisibleWeeks < SkUiCalendarGrid.MaxWeeks)
            return false;
        if (_system is null)
            return SkUiCalendarGrid.IsOtherMonth(start, day);
        MonthsAround(start);
        return day < _months.Start || day >= _months.Next;
    }

    private bool PageContains(DateTime date)
    {
        var start = PageStart;
        if (_mode == SkUiCalendarDisplayMode.Month)
            return date >= start && date <= SkUiCalendarGrid.PageEnd(start, _mode, VisibleWeeks, _system);
        return SkUiCalendarGrid.PeriodIndex(start, _mode, date, _system) is var index and >= 0 && !SkUiCalendarGrid.IsAdjacentPeriodCell(_mode, index);
    }

    #endregion

    #region Input

    /// <summary>A pointer went down at <paramref name="point"/>: the part under it shows pressed.</summary>
    public void Press(Point point, float width, float height)
    {
        _keyboard = false;
        var hit = HitTest(point, width, height);
        if (!hit.IsEnabled || IsAnimating)
            hit = default;
        if (hit == _pressed)
            return;
        _pressed = hit;
        host.CalendarInvalidate();
    }

    /// <summary>The pointer went up or moved away: nothing shows pressed.</summary>
    public void Unpress()
    {
        if (_pressed.Part == SkUiCalendarPart.None)
            return;
        _pressed = default;
        host.CalendarInvalidate();
    }

    /// <summary>A tap at <paramref name="point"/>: the title changes the mode, the buttons move the page, a cell is chosen or opened.</summary>
    public void Tap(Point point, float width, float height)
    {
        var hit = HitTest(point, width, height);
        switch (hit.Part)
        {
            case SkUiCalendarPart.Title when hit.IsEnabled:
                StepUpMode();
                break;
            case SkUiCalendarPart.Previous:
                Step(-1, animated: true);
                break;
            case SkUiCalendarPart.Next:
                Step(1, animated: true);
                break;
            case SkUiCalendarPart.Cell:
                Activate(hit.Date, hit.IsEnabled, fromKeyboard: false);
                break;
            case SkUiCalendarPart.WeekNumber when host.CalendarEnabled:
                host.CalendarWeekNumberTapped(WeekArgs(hit.Date));
                break;
        }
    }

    /// <summary>What a tap on the week number of the week starting at <paramref name="weekStart"/> reports.</summary>
    private SkUiCalendarWeekNumberTappedEventArgs WeekArgs(DateTime weekStart)
    {
        var end = SkUiCalendarGrid.AddDays(weekStart, 6) ?? weekStart;
        return new SkUiCalendarWeekNumberTappedEventArgs(weekStart, end,
            SkUiCalendarGrid.WeekNumber(weekStart, EffectiveFirstDayOfWeek, WeekCulture), FirstEnabledDay(weekStart, end),
            LastEnabledDay(weekStart, end));
    }

    /// <summary>The first day from <paramref name="from"/> to <paramref name="to"/> that can be chosen, or <c>null</c>.</summary>
    internal DateTime? FirstEnabledDay(DateTime from, DateTime to)
    {
        for (var offset = 0; offset <= (to - from).Days; offset++)
            if (from.AddDays(offset) is var day && IsDayEnabled(day))
                return day;
        return null;
    }

    /// <summary>The last day from <paramref name="from"/> to <paramref name="to"/> that can be chosen, or <c>null</c>.</summary>
    internal DateTime? LastEnabledDay(DateTime from, DateTime to)
    {
        for (var offset = 0; offset <= (to - from).Days; offset++)
            if (to.AddDays(-offset) is var day && IsDayEnabled(day))
                return day;
        return null;
    }

    /// <summary>A cell was tapped or activated: the tap is reported; an enabled day is chosen (and its month shown), a period opens or is chosen.</summary>
    private void Activate(DateTime date, bool enabled, bool fromKeyboard)
    {
        var mode = _mode;
        host.CalendarDateTapped(new SkUiCalendarDateTappedEventArgs(date, mode, enabled));
        if (!enabled || !host.CalendarEnabled)
            return;
        _focus = date;
        if (mode > MinimumDisplayMode)
        {
            StepDownMode(date);
            return;
        }
        if (mode == SkUiCalendarDisplayMode.Month && !PageContains(date) && !fromKeyboard)
        {
            // A neighbor month's day: its month slides in.
            var before = PageStart;
            _display = ClampToRange(date);
            StartSlide(VisualPages(PageStart > before ? 1 : -1), from: null);
            host.CalendarDisplayChanged();
        }
        Choose(date);
        host.CalendarInvalidate();
    }

    /// <summary>Whether a swipe can start now (enabled, no zoom running).</summary>
    public bool CanSwipe => IsSwipeEnabled && host.CalendarEnabled && _zoom is not { IsRunning: true };

    /// <summary>A swipe started: the page follows the pointer.</summary>
    public void BeginDrag()
    {
        Unpress();
        _drag = _dragFrom = SlideOffset;
        _dragging = true;
        _slide?.Jump(0);
    }

    /// <summary>The page extent a swipe moves across: the grid area's width, or its height for vertical navigation.</summary>
    private float Extent(float width, float height)
    {
        var frame = Layout(width, height, SkUiLook.Current);
        return NavigationOrientation == StackOrientation.Horizontal ? width : Math.Max(1, height - frame.GridTop(_mode == SkUiCalendarDisplayMode.Month));
    }

    /// <summary>The swipe moved by <paramref name="distance"/> DIPs along the navigation axis since it started.</summary>
    public void Drag(double distance, float width, float height)
    {
        if (!_dragging)
            return;
        var extent = Math.Max(1, Extent(width, height));
        // From where a running slide was caught, so the page does not jump under the finger.
        var offset = _dragFrom + (float)(distance / extent);
        // Past the first or last page the page moves less (a rubber band), and never by a whole page: only the part past the
        // middle (or past where the drag started, already there) toward a page that cannot come.
        if (offset != 0 && !CanStep(LogicalPages(offset < 0 ? 1 : -1)))
        {
            var edge = Math.Sign(_dragFrom) == Math.Sign(offset) ? _dragFrom : 0;
            if (Math.Abs(offset) > Math.Abs(edge))
                offset = edge + (offset - edge) * EdgeResistance;
        }
        _drag = Math.Clamp(offset, -1, 1);
        host.CalendarInvalidate();
    }

    /// <summary>The logical pages (next = 1) for a move of <paramref name="visualPages"/> visual pages (the inverse of <see cref="VisualPages"/>).</summary>
    private int LogicalPages(int visualPages) => VisualPages(visualPages);

    /// <summary>The swipe ended at <paramref name="velocity"/> DIPs per second: the page settles on the neighbor or back.</summary>
    public void EndDrag(double velocity, float width, float height)
    {
        if (!_dragging)
            return;
        _dragging = false;
        var offset = _drag;
        _drag = 0;
        // The swipe's own move decides (a slide it caught is not part of it); the offset is where the pages are.
        var moved = offset - _dragFrom;
        var visual = 0;
        if (moved <= -SettleShare || velocity <= -FlingVelocity && moved < 0)
            visual = 1; // the content moved back: the page after it (to the right or below) comes in
        else if (moved >= SettleShare || velocity >= FlingVelocity && moved > 0)
            visual = -1;
        var pages = LogicalPages(visual);
        if (visual != 0 && CanStep(pages) && StepFrom(PageStart, pages) is { } target)
        {
            _display = ClampToRange(target);
            // The new page is where the old one's neighbor was, a page away from the offset.
            StartSlide(visual, from: Math.Clamp(offset + visual, -1, 1));
            host.CalendarDisplayChanged();
        }
        else
            StartSlide(0, from: offset);
        host.CalendarInvalidate();
    }

    /// <summary>The swipe was taken by something else: the page goes back.</summary>
    public void CancelDrag()
    {
        if (!_dragging)
            return;
        _dragging = false;
        var offset = _drag;
        _drag = 0;
        StartSlide(0, from: offset);
        host.CalendarInvalidate();
    }

    /// <summary>Handles a key: arrows move the focus, Page Up / Down the page (with Shift a year), Home / End the week's ends, Enter / Space choose.</summary>
    public bool OnKey(in SkUiKeyEvent key)
    {
        if (!host.CalendarEnabled || (key.HasCommandModifier && key.Key is not (SkUiKey.PageUp or SkUiKey.PageDown)))
            return false;
        var rtl = host.CalendarRightToLeft;
        var month = _mode == SkUiCalendarDisplayMode.Month;
        var row = month ? 7 : SkUiCalendarGrid.PeriodColumns;
        var focus = EnsureFocus();
        var shift = (key.Modifiers & SkUiKeyModifiers.Shift) != 0;
        DateTime? target = key.Key switch
        {
            SkUiKey.Left => MoveCells(focus, rtl ? 1 : -1),
            SkUiKey.Right => MoveCells(focus, rtl ? -1 : 1),
            SkUiKey.Up => MoveCells(focus, -row),
            SkUiKey.Down => MoveCells(focus, row),
            SkUiKey.PageUp => shift && month ? SkUiCalendarGrid.AddYears(focus, -1, _system) : MovePages(focus, -1),
            SkUiKey.PageDown => shift && month ? SkUiCalendarGrid.AddYears(focus, 1, _system) : MovePages(focus, 1),
            SkUiKey.Home when month => SkUiCalendarGrid.WeekStart(focus, EffectiveFirstDayOfWeek),
            SkUiKey.End when month => SkUiCalendarGrid.AddDays(SkUiCalendarGrid.WeekStart(focus, EffectiveFirstDayOfWeek), 6),
            _ => null
        };
        // Only a key the calendar handles shows the focus ring.
        if (key.Key is SkUiKey.Enter or SkUiKey.Space)
        {
            _keyboard = true;
            Activate(focus, month ? IsChoosable(focus) : IsPeriodEnabled(focus, _mode), fromKeyboard: true);
            return true;
        }
        if (target is not { } next)
            return false;
        _keyboard = true;
        MoveFocus(next);
        return true;
    }

    /// <summary>The day keys and screen readers act on: the focus, else the selection or today on the page, else the page's first day.</summary>
    private DateTime EnsureFocus()
    {
        if (_focus is { } focus && PageContains(focus))
            return focus;
        var start = PageStart;
        DateTime chosen;
        if (_mode == SkUiCalendarDisplayMode.Month)
            chosen = _selectedDate is { } selected && PageContains(selected) ? selected
                : PageContains(DateTime.Today) ? DateTime.Today : start;
        else
        {
            var today = DateTime.Today;
            var index = SkUiCalendarGrid.PeriodIndex(start, _mode, _display, _system);
            if (index < 0 || SkUiCalendarGrid.IsAdjacentPeriodCell(_mode, index))
                index = SkUiCalendarGrid.PeriodIndex(start, _mode, today, _system) is var todayIndex and >= 0 && !SkUiCalendarGrid.IsAdjacentPeriodCell(_mode, todayIndex) ? todayIndex : _mode == SkUiCalendarDisplayMode.Year ? 0 : 1;
            chosen = SkUiCalendarGrid.PeriodCell(start, _mode, index, _system) ?? start;
        }
        _focus = ClampToRange(chosen);
        return _focus.Value;
    }

    /// <summary><paramref name="focus"/> moved by <paramref name="cells"/> cells: days, months, years or decades by the mode.</summary>
    private DateTime? MoveCells(DateTime focus, int cells) => SkUiCalendarGrid.MoveCells(focus, _mode, cells, _system);

    private DateTime? MovePages(DateTime focus, int pages) => _mode switch
    {
        SkUiCalendarDisplayMode.Month when VisibleWeeks >= SkUiCalendarGrid.MaxWeeks => SkUiCalendarGrid.AddMonths(focus, pages, _system),
        SkUiCalendarDisplayMode.Month => SkUiCalendarGrid.AddDays(focus, 7L * VisibleWeeks * pages),
        SkUiCalendarDisplayMode.Year => SkUiCalendarGrid.AddYears(focus, pages, _system),
        SkUiCalendarDisplayMode.Decade => SkUiCalendarGrid.MoveYears(focus, 10L * pages, _system),
        _ => SkUiCalendarGrid.MoveYears(focus, 100L * pages, _system)
    };

    /// <summary>Puts the focus on <paramref name="date"/> (kept in the range); a page holding it slides in.</summary>
    private void MoveFocus(DateTime date)
    {
        var day = ClampToRange(date);
        _focus = day;
        if (!PageContains(day))
        {
            var before = PageStart;
            _display = day;
            var after = PageStart;
            if (after != before)
            {
                StartSlide(VisualPages(after > before ? 1 : -1), from: null);
                host.CalendarDisplayChanged();
            }
        }
        host.CalendarInvalidate();
    }

    /// <summary>Screen readers' actions: increment / decrement move the focus a cell, activate chooses it. Returns whether it acted.</summary>
    public bool OnSemanticsAction(SkUiSemanticsActions action)
    {
        if (!host.CalendarEnabled)
            return false;
        var focus = EnsureFocus();
        switch (action)
        {
            case SkUiSemanticsActions.Increment when MoveCells(focus, 1) is { } next:
                MoveFocus(next);
                return true;
            case SkUiSemanticsActions.Decrement when MoveCells(focus, -1) is { } previous:
                MoveFocus(previous);
                return true;
            case SkUiSemanticsActions.Activate:
                Activate(focus, _mode == SkUiCalendarDisplayMode.Month ? IsChoosable(focus) : IsPeriodEnabled(focus, _mode), fromKeyboard: true);
                return true;
            default:
                return false;
        }
    }

    /// <summary>The calendar as one adjustable element: the title as its name, the focused cell and its state as its value.</summary>
    public void PopulateSemantics(SkUiSemanticsInfo info)
    {
        info.Role = SkUiSemanticsRole.Slider;
        info.IsHorizontal = true;
        info.Text = Title;
        var focus = _focus is { } current && PageContains(current) ? current : EnsureFocus();
        info.Value = Describe(focus);
        if (host.CalendarEnabled)
            info.Actions |= SkUiSemanticsActions.Increment | SkUiSemanticsActions.Decrement | SkUiSemanticsActions.Activate;
        AddParts(info, Culture, SkUiStrings.Current);
    }

    private const int TitlePart = 0;
    private const int PreviousPart = 1;
    private const int NextPart = 2;
    private const int FirstCellPart = 3;

    // The element ids of the header's parts and the page's cells, kept while the calendar lives (a cell's id stays with its place).
    private int[]? _partIds;

    private int PartId(int part)
    {
        _partIds ??= new int[FirstCellPart + SkUiCalendarGrid.MaxWeeks * 7];
        ref var id = ref _partIds[part];
        if (id == 0)
            id = SkUiSemanticsTree.NewId();
        return id;
    }

    /// <summary>
    /// The header's title and buttons and every cell of the page as elements of their own (children of the calendar's element):
    /// a day reads its date with its state, and activating it chooses it; the title changes the display mode, the buttons the page.
    /// </summary>
    private void AddParts(SkUiSemanticsInfo info, CultureInfo culture, SkUiStrings strings)
    {
        var size = host.CalendarSize;
        if (size.Width <= 0 || size.Height <= 0)
            return;
        var look = SkUiLook.Current;
        var frame = Layout((float)size.Width, (float)size.Height, look);
        var (title, previous, next) = HeaderRects(frame, look);
        if (!title.IsEmpty)
            info.AddPart(new SkUiSemanticsPart(PartId(TitlePart), TitlePart, ToRect(title), Title, "_Title", CanChangeMode));
        if (!previous.IsEmpty)
            info.AddPart(new SkUiSemanticsPart(PartId(PreviousPart), PreviousPart, ToRect(previous), strings.PreviousText, "_Previous", CanStep(-1)));
        if (!next.IsEmpty)
            info.AddPart(new SkUiSemanticsPart(PartId(NextPart), NextPart, ToRect(next), strings.NextText, "_Next", CanStep(1)));
        var month = _mode == SkUiCalendarDisplayMode.Month;
        var start = PageStart;
        var columns = month ? 7 : SkUiCalendarGrid.PeriodColumns;
        var rows = month ? Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks) : SkUiCalendarGrid.PeriodRowCount(_mode, _system);
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var index = row * columns + column;
                var hit = month ? DayHit(start, index) : PeriodHit(start, index);
                if (hit.Part != SkUiCalendarPart.Cell)
                    continue;
                var label = month ? hit.Date.ToString(SkUiCalendarGrid.LongDatePattern(culture, _system), culture)
                    : _mode == SkUiCalendarDisplayMode.Year ? hit.Date.ToString(SkUiCalendarGrid.YearMonthPattern(culture, _system), culture)
                    : SkUiCalendarGrid.PeriodText(hit.Date, _mode, culture, _system);
                info.AddPart(new SkUiSemanticsPart(PartId(FirstCellPart + index), FirstCellPart + index, ToRect(CellRect(frame, month, row, column)), label,
                    string.Concat("_", hit.Date.ToString(month ? "yyyy-MM-dd" : "yyyy-MM", CultureInfo.InvariantCulture)), hit.IsEnabled, State(hit.Date, month, strings)));
            }
    }

    private static Rect ToRect(SKRect rect) => new(rect.Left, rect.Top, rect.Width, rect.Height);

    /// <summary>What a cell's element reads after its date: today, selected, unavailable.</summary>
    private string? State(DateTime date, bool month, SkUiStrings strings)
    {
        if (!month)
            return IsSelectedPeriod(date, _mode) ? strings.SelectedStateText : null;
        var today = date == DateTime.Today;
        var selected = IsSelectedDay(date) || RangeOf(date) is not null;
        var state = (today, selected) switch
        {
            (true, true) => string.Concat(strings.SelectedStateText, ", ", strings.TodayStateText),
            (true, false) => strings.TodayStateText,
            (false, true) => strings.SelectedStateText,
            _ => null
        };
        return Captions(date) is { } captions ? state is null ? captions : string.Concat(state, ", ", captions) : state;
    }

    /// <summary>The captions of a day's markers (its events), as screen readers read them after its state, or <c>null</c> (1.15).</summary>
    private string? Captions(DateTime date)
    {
        if (StyleOf(date)?.Markers is not { Count: > 0 } markers)
            return null;
        string? captions = null;
        foreach (var marker in markers)
            if (marker.Kind == SkUiCalendarMarkerKind.Caption && marker.Text is { Length: > 0 } text)
                captions = captions is null ? text : string.Concat(captions, ", ", text);
        return captions;
    }

    /// <summary>A screen reader activated a part (<see cref="AddParts"/>); returns whether it acted.</summary>
    public bool OnSemanticsPartAction(int part, SkUiSemanticsActions action)
    {
        if (action != SkUiSemanticsActions.Activate || !host.CalendarEnabled)
            return false;
        switch (part)
        {
            case TitlePart when CanChangeMode:
                StepUpMode();
                return true;
            case PreviousPart:
                return Step(-1, animated: true);
            case NextPart:
                return Step(1, animated: true);
            case >= FirstCellPart:
                var month = _mode == SkUiCalendarDisplayMode.Month;
                var hit = month ? DayHit(PageStart, part - FirstCellPart) : PeriodHit(PageStart, part - FirstCellPart);
                if (hit.Part != SkUiCalendarPart.Cell)
                    return false;
                Activate(hit.Date, hit.IsEnabled, fromKeyboard: true);
                return true;
            default:
                return false;
        }
    }

    private string Describe(DateTime date)
    {
        var culture = Culture;
        var strings = SkUiStrings.Current;
        if (_mode != SkUiCalendarDisplayMode.Month)
        {
            var text = _mode == SkUiCalendarDisplayMode.Year ? date.ToString(SkUiCalendarGrid.YearMonthPattern(culture, _system), culture) : SkUiCalendarGrid.PeriodText(date, _mode, culture, _system);
            return IsPeriodEnabled(date, _mode) ? text : string.Concat(text, ", ", strings.UnavailableStateText);
        }
        var parts = new List<string>(4) { date.ToString(SkUiCalendarGrid.LongDatePattern(culture, _system), culture) };
        if (date == DateTime.Today)
            parts.Add(strings.TodayStateText);
        if (IsSelectedDay(date) || RangeOf(date) is not null)
            parts.Add(strings.SelectedStateText);
        if (!IsChoosable(date))
            parts.Add(strings.UnavailableStateText);
        if (Captions(date) is { } captions)
            parts.Add(captions);
        return string.Join(", ", parts);
    }

    #endregion

    #region Drawing

    /// <summary>The colors of one drawing, resolved from the settings and the color scheme (dimmed when disabled).</summary>
    private readonly record struct Palette(SKColor Text, SKColor Adjacent, SKColor Disabled, SKColor Weekend, SKColor DayName, SKColor Header,
        SKColor Selection, SKColor SelectedText, SKColor Range, SKColor RangeText, SKColor Today, SKColor TodayText, SKColor Button,
        SKColor ButtonBackground, SKColor Focus, SKColor TodayDayName, SKColor ButtonBorder);

    private Palette ResolvePalette(bool enabled, SkUiLook look)
    {
        var scheme = SkUiColorScheme.Current;
        var text = TextColor ?? scheme.DefaultForeground;
        var selection = SelectionColor ?? scheme.Accent;
        var today = TodayColor ?? scheme.Accent;
        // The selection's fill and number: the app's, else the look's (a light tint with an accent number on iOS), from the
        // selection color the range band is made from too.
        var baseColor = Sk(selection, true);
        var fill = SelectionColor is not null ? baseColor : look.CalendarSelectionColor(baseColor);
        var selectedText = SelectedTextColor is { } own ? Sk(own, enabled) : Tint(look.CalendarSelectedTextColor(fill, baseColor, Sk(text, true)), enabled);
        var todayText = TodayTextColor ?? TodayStyle switch
        {
            SkUiCalendarTodayStyle.Filled => Contrast(today),
            SkUiCalendarTodayStyle.Text => today,
            _ => text
        };
        return new Palette(
            Sk(text, enabled), Sk(AdjacentDayTextColor ?? scheme.Muted, enabled), Sk(DisabledDayTextColor ?? scheme.Disabled, enabled),
            Sk(WeekendTextColor ?? text, enabled), Sk(DayNameTextColor ?? scheme.Muted, enabled), Sk(HeaderTextColor ?? text, enabled),
            Tint(fill, enabled), selectedText, RangeColor is { } range ? Sk(range, enabled) : Sk(selection, enabled, RangeAlpha),
            Sk(RangeTextColor ?? text, enabled), Sk(today, enabled), Sk(todayText, enabled), Sk(NavigationButtonColor ?? scheme.Accent, enabled),
            Sk(NavigationButtonBackgroundColor ?? Colors.Transparent, enabled), Sk(scheme.DefaultForeground, true),
            Sk(TodayDayNameTextColor ?? today, enabled), Sk(NavigationButtonBorderColor ?? Colors.Transparent, enabled));
    }

    /// <summary>The range band's alpha share of the selection color when <c>RangeColor</c> is not set.</summary>
    private const float RangeAlpha = 0.22f;

    private static readonly Color NearBlack = Color.FromRgb(0x1C, 0x1C, 0x1E);

    /// <summary>
    /// <paramref name="color"/> as drawn: its alpha times <paramref name="alpha"/>, halved while disabled (as
    /// <see cref="SkUiColors.EnabledTint"/>). On the Skia color, so a frame allocates no MAUI colors.
    /// </summary>
    private static SKColor Sk(Color color, bool enabled, float alpha = 1) => Tint(SkUiShapePainter.ToSkColor(color), enabled, alpha);

    /// <summary>A Skia color as drawn: its alpha times <paramref name="alpha"/>, halved while disabled.</summary>
    private static SKColor Tint(SKColor sk, bool enabled, float alpha = 1)
    {
        var scale = enabled ? alpha : alpha * 0.5f;
        return scale == 1 ? sk : sk.WithAlpha((byte)Math.Round(sk.Alpha * scale)); // exact: 1 is the "unchanged" sentinel
    }

    /// <summary>White or near black, whichever reads on <paramref name="fill"/>.</summary>
    private static Color Contrast(Color fill) => 0.299 * fill.Red + 0.587 * fill.Green + 0.114 * fill.Blue > 0.62 ? NearBlack : Colors.White;

    /// <summary>The text styles of one drawing (font scaling and the look read once).</summary>
    private readonly record struct Styles(SkUiTextStyle Day, SkUiTextStyle DayName, SkUiTextStyle Title, SkUiTextStyle WeekNumber, SkUiTextDirection Direction);

    private Styles ResolveStyles(SkUiLook look, bool rtl)
    {
        var direction = rtl ? SkUiTextDirection.RightToLeft : SkUiTextDirection.LeftToRight;
        return new Styles(
            TextStyle(FontAttributes, Size(FontSize, look.CalendarFontSize), direction),
            TextStyle(DayNameFontAttributes, Size(DayNameFontSize, look.CalendarDayNameFontSize), direction),
            TextStyle(HeaderFontAttributes, Size(HeaderFontSize, look.CalendarHeaderFontSize), direction, LineBreakMode.TailTruncation, 0.8f, HeaderFontFamily),
            TextStyle(FontAttributes.None, Size(DayNameFontSize, look.CalendarDayNameFontSize), direction),
            direction);
    }

    private double Size(double set, double fallback) => SkUiFontScaling.ScaleFontSize(double.IsNaN(set) ? fallback : set, FontAutoScalingEnabled);

    private SkUiTextStyle TextStyle(FontAttributes attributes, double size, SkUiTextDirection direction, LineBreakMode mode = LineBreakMode.NoWrap,
        float minimumScale = 0.6f, string? family = null) =>
        new(SkUiTypefaces.Resolve(family ?? FontFamily, attributes), size, mode, null, -1, -1, 0, direction, SkUiTextRendering.Default, attributes,
            MinimumScale: minimumScale);

    private SkUiTextStyle WithAttributes(in SkUiTextStyle style, FontAttributes attributes) => attributes == style.FontAttributes
        ? style
        : style with { Typeface = SkUiTypefaces.Resolve(FontFamily, attributes), FontAttributes = attributes };

    /// <summary>Draws the calendar into a <paramref name="width"/> × <paramref name="height"/> node.</summary>
    public void Draw(SKCanvas canvas, float width, float height)
    {
        if (width <= 0 || height <= 0)
            return;
        var look = SkUiLook.Current;
        var culture = Culture;
        var enabled = host.CalendarEnabled;
        var frame = Layout(width, height, look);
        var palette = ResolvePalette(enabled, look);
        var styles = ResolveStyles(look, frame.RightToLeft);
        var focusVisible = _keyboard && _focus is not null;
        DrawHeader(canvas, frame, look, palette, styles);

        var save = canvas.Save();
        canvas.ClipRect(frame.GridArea);
        if (_zoom is { IsRunning: true } zoom)
        {
            // The page left fades and scales away; the new one fades and scales in. Zooming out, both shrink. (No lambdas in
            // this method: their closure would be allocated on every draw, also on the frames of a swipe.)
            var t = Math.Clamp(zoom.Value, 0, 1);
            var zoomingOut = _mode > _zoomFromMode;
            var fromScale = zoomingOut ? 1 - ZoomScale * t : 1 + ZoomScale * t;
            var toScale = zoomingOut ? 1 + ZoomScale * (1 - t) : 1 - ZoomScale * (1 - t);
            if (BeginLayer(canvas, frame, 1 - t, fromScale) is var fromSave and >= 0)
            {
                DrawPage(canvas, frame, _zoomFromMode, _zoomFromStart, 0, 0, palette, styles, look, culture, false, withDayNames: true);
                canvas.RestoreToCount(fromSave);
            }
            if (BeginLayer(canvas, frame, t, toScale) is var toSave and >= 0)
            {
                DrawPage(canvas, frame, _mode, PageStart, 0, 0, palette, styles, look, culture, focusVisible, withDayNames: true);
                canvas.RestoreToCount(toSave);
            }
        }
        else
        {
            var month = _mode == SkUiCalendarDisplayMode.Month;
            if (month)
                DrawDayNames(canvas, frame, PageStart, palette, styles, culture);
            var offset = SlideOffset;
            var start = PageStart;
            var horizontal = NavigationOrientation == StackOrientation.Horizontal;
            var extent = horizontal ? width : Math.Max(1, height - frame.GridTop(month));
            if (month)
            {
                // Pages slide under the day names, not over them.
                canvas.ClipRect(new SKRect(0, frame.GridTop(true), width, height));
            }
            DrawPage(canvas, frame, _mode, start, horizontal ? offset * extent : 0, horizontal ? 0 : offset * extent, palette, styles, look, culture,
                focusVisible, withDayNames: false);
            if (offset != 0)
            {
                // The neighbor on the side the page moved away from.
                var visual = offset < 0 ? 1 : -1;
                if (StepFrom(start, LogicalPages(visual)) is { } neighbor)
                {
                    var neighborOffset = (offset + visual) * extent;
                    DrawPage(canvas, frame, _mode, neighbor, horizontal ? neighborOffset : 0, horizontal ? 0 : neighborOffset, palette, styles, look,
                        culture, false, withDayNames: false);
                }
            }
        }
        canvas.RestoreToCount(save);
    }

    /// <summary>Starts a faded, scaled layer over the grid area and returns its save count to restore, or -1 when it is not visible.</summary>
    private int BeginLayer(SKCanvas canvas, in Frame frame, float alpha, float scale)
    {
        if (alpha <= 0.001f)
            return -1;
        var paint = _layerPaint ??= new SKPaint();
        paint.Color = SKColors.Black.WithAlpha((byte)Math.Clamp(alpha * 255, 0, 255));
        var area = frame.GridArea;
        var save = canvas.SaveLayer(area, paint);
        canvas.Scale(scale, scale, area.MidX, area.MidY);
        return save;
    }

    private void DrawHeader(SKCanvas canvas, in Frame frame, SkUiLook look, in Palette palette, in Styles styles)
    {
        if (frame.HeaderHeight <= 0)
            return;
        var (titleRect, previous, next) = HeaderRects(frame, look);
        var alignment = HeaderLayout == SkUiCalendarHeaderLayout.Centered ? TextAlignment.Center : TextAlignment.Start;
        var title = Title;
        var pressedTitle = _pressed.Part == SkUiCalendarPart.Title;
        var color = pressedTitle ? palette.Header.WithAlpha((byte)(palette.Header.Alpha * 0.6f)) : palette.Header;
        if (!host.CalendarDrawTitle(canvas, titleRect, title, palette.Header, pressedTitle))
            DrawText(canvas, title, SlotTitle, styles.Title, titleRect, color, alignment);
        // Sideways pages: left / right chevrons, mirrored right-to-left (the app's icons too); vertical pages: up / down, never mirrored.
        var vertical = NavigationOrientation == StackOrientation.Vertical;
        var mirrored = !vertical && frame.RightToLeft;
        DrawNavigationButton(canvas, look, palette, previous, isNext: false,
            vertical ? SkUiCalendarChevronDirection.Up : mirrored ? SkUiCalendarChevronDirection.Right : SkUiCalendarChevronDirection.Left, mirrored,
            _pressed.Part == SkUiCalendarPart.Previous, CanStep(-1) && host.CalendarEnabled);
        DrawNavigationButton(canvas, look, palette, next, isNext: true,
            vertical ? SkUiCalendarChevronDirection.Down : mirrored ? SkUiCalendarChevronDirection.Left : SkUiCalendarChevronDirection.Right, mirrored,
            _pressed.Part == SkUiCalendarPart.Next, CanStep(1) && host.CalendarEnabled);
    }

    /// <summary>
    /// A header button: a subclass's drawing, else the look's background, border and (without an icon or glyph) chevron, with
    /// the app's image or glyph over it, mirrored right-to-left while the pages move sideways (the previous button then points the
    /// other way), dimmed while pressed or disabled.
    /// </summary>
    private void DrawNavigationButton(SKCanvas canvas, SkUiLook look, in Palette palette, SKRect bounds, bool isNext,
        SkUiCalendarChevronDirection direction, bool rightToLeft,
        bool pressed, bool enabled)
    {
        if (bounds.IsEmpty)
            return;
        var slot = isNext ? _nextSlot : _previousSlot;
        var image = (isNext ? _nextIcon : _previousIcon) is not null && slot?.Image is not null ? slot : null;
        var glyph = isNext ? NextButtonGlyph : PreviousButtonGlyph;
        var hasGlyph = image is null && !string.IsNullOrEmpty(glyph);
        var paint = new SkUiCalendarNavigationButtonPaint(bounds, direction, palette.Button, palette.ButtonBackground, pressed, enabled)
        {
            CornerRadius = NavigationButtonCornerRadius < 0 ? -1 : (float)NavigationButtonCornerRadius,
            BorderColor = palette.ButtonBorder,
            BorderWidth = (float)Math.Max(0, NavigationButtonBorderWidth),
            // An icon still loading shows no chevron: the button would change its picture once the image arrives.
            ShowsChevron = image is null && !hasGlyph && (isNext ? _nextIcon : _previousIcon) is null
        };
        if (host.CalendarDrawNavigationButton(canvas, paint, isNext))
            return;
        look.DrawCalendarNavigationButton(canvas, paint);
        if (image is null && !hasGlyph)
            return;
        var alpha = !enabled ? 0.35f : pressed ? 0.6f : 1f;
        var size = (float)(double.IsNaN(NavigationButtonGlyphSize) ? bounds.Height * 0.5 : NavigationButtonGlyphSize);
        var save = canvas.Save();
        if (rightToLeft)
            canvas.Scale(-1, 1, bounds.MidX, 0);
        if (image is not null)
        {
            var area = SKRect.Create(bounds.MidX - size / 2, bounds.MidY - size / 2, size, size);
            if (alpha < 1)
            {
                var layer = _layerPaint ??= new SKPaint();
                layer.Color = SKColors.Black.WithAlpha((byte)(alpha * 255));
                canvas.SaveLayer(area, layer);
            }
            image.Paint(canvas, area, Aspect.AspectFit);
        }
        else
            DrawCustomText(canvas, glyph!, bounds, palette.Button.WithAlpha((byte)(palette.Button.Alpha * alpha)), size, FontAttributes.None,
                NavigationButtonFontFamily, TextAlignment.Center);
        canvas.RestoreToCount(save);
    }

    internal string[] DayNames(DayOfWeek first, CultureInfo culture)
    {
        var strings = SkUiStrings.Current;
        var key = (first, DayNameFormat, culture, strings, SkUiStrings.Version);
        if (_dayNames is not null && key == _dayNamesKey)
            return _dayNames;
        _dayNamesKey = key;
        var names = new string[7];
        for (var column = 0; column < 7; column++)
            names[column] = strings.GetDayName((DayOfWeek)(((int)first + column) % 7), DayNameFormat, culture);
        return _dayNames = names;
    }

    private void DrawDayNames(SKCanvas canvas, in Frame frame, DateTime start, in Palette palette, in Styles styles, CultureInfo culture, float dx = 0,
        float dy = 0)
    {
        if (frame.DayNamesHeight <= 0)
            return;
        var first = EffectiveFirstDayOfWeek;
        var names = DayNames(first, culture);
        var todayColumn = TodayColumn(start, first);
        for (var column = 0; column < 7; column++)
        {
            var cell = CellRect(frame, month: true, 0, column);
            var rect = SKRect.Create(cell.Left + dx, frame.HeaderHeight + dy, cell.Width, frame.DayNamesHeight);
            DrawText(canvas, names[column], SlotDayName, styles.DayName, rect, column == todayColumn ? palette.TodayDayName : palette.DayName,
                TextAlignment.Center);
        }
    }

    /// <summary>The column of today's day name when today is one of the days the month page starting at <paramref name="start"/> shows, else -1.</summary>
    internal int TodayColumn(DateTime start, DayOfWeek first)
    {
        if (!IsTodayDayNameHighlighted)
            return -1;
        var today = DateTime.Today;
        var gridStart = SkUiCalendarGrid.GridStart(start, VisibleWeeks, first);
        var days = (today - gridStart).Days;
        if (days < 0 || days >= 7 * Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks) || IsAdjacent(start, today) && !AreAdjacentDaysVisible)
            return -1;
        return ((int)today.DayOfWeek - (int)first + 7) % 7;
    }

    private void DrawPage(SKCanvas canvas, in Frame frame, SkUiCalendarDisplayMode mode, DateTime start, float dx, float dy, in Palette palette,
        in Styles styles, SkUiLook look, CultureInfo culture, bool focusVisible, bool withDayNames)
    {
        // The page moves by a canvas translation, so its cells keep the same rects on every frame of a slide: a text drawn in a
        // rect whose width changed by a rounding error (a shortened caption) would be laid out again.
        var save = canvas.Save();
        canvas.Translate(dx, dy);
        if (mode == SkUiCalendarDisplayMode.Month)
        {
            if (withDayNames)
                DrawDayNames(canvas, frame, start, palette, styles, culture);
            DrawMonthPage(canvas, frame, start, 0, 0, palette, styles, look, culture, focusVisible);
        }
        else
            DrawPeriodPage(canvas, frame, mode, start, 0, 0, palette, styles, look, culture, focusVisible);
        canvas.RestoreToCount(save);
    }

    private void DrawMonthPage(SKCanvas canvas, in Frame frame, DateTime start, float dx, float dy, in Palette palette, in Styles styles,
        SkUiLook look, CultureInfo culture, bool focusVisible)
    {
        var first = EffectiveFirstDayOfWeek;
        var gridStart = SkUiCalendarGrid.GridStart(start, VisibleWeeks, first);
        var rows = Math.Clamp(VisibleWeeks, 1, SkUiCalendarGrid.MaxWeeks);
        var today = DateTime.Today;
        var enabled = host.CalendarEnabled;
        for (var row = 0; row < rows; row++)
        {
            if (frame.WeekColumn > 0 && SkUiCalendarGrid.AddDays(gridStart, row * 7) is { } weekStart)
            {
                var rect = WeekNumberRect(frame, row);
                rect.Offset(dx, dy);
                DrawText(canvas, Numbers[SkUiCalendarGrid.WeekNumber(weekStart, first, WeekCulture)], SlotWeekNumber, styles.WeekNumber, rect, palette.DayName,
                    TextAlignment.Center);
            }
            for (var column = 0; column < 7; column++)
            {
                if (SkUiCalendarGrid.AddDays(gridStart, row * 7 + column) is not { } day)
                    continue;
                var number = DayNumber(start, day);
                if (number < 0)
                    continue; // before or after the days the calendar system supports
                var adjacent = IsAdjacent(start, day);
                if (adjacent && !AreAdjacentDaysVisible)
                    continue;
                var cell = CellRect(frame, month: true, row, column);
                cell.Offset(dx, dy);
                var dayEnabled = IsChoosable(day);
                var style = StyleOf(day);
                // A neighbor month's day shown on the page is drawn selected or in the range too, so a range runs on across the month's end.
                var part = SkUiCalendarRangePart.None;
                bool toLeft = false, toRight = false;
                var selected = IsSelectedDay(day);
                if (RangeOf(day) is { } r)
                {
                    part = r.Start == r.LastDay ? SkUiCalendarRangePart.StartAndEnd
                        : day == r.Start ? SkUiCalendarRangePart.Start
                        : day == r.LastDay ? SkUiCalendarRangePart.End
                        : SkUiCalendarRangePart.Middle;
                    // The band runs toward the next day (after) and the previous one (before), mirrored right-to-left.
                    var after = day < r.LastDay;
                    var before = day > r.Start;
                    (toLeft, toRight) = frame.RightToLeft ? (after, before) : (before, after);
                }
                var isToday = day == today && !adjacent && TodayStyle != SkUiCalendarTodayStyle.None;
                var focused = focusVisible && _focus == day;
                // The number and the look's chrome take the cell above the marker area.
                var marker = frame.EventNumber > 0 ? Math.Max(0, cell.Height - frame.EventNumber)
                    : frame.MarkerArea > 0 && cell.Height > frame.MarkerArea ? frame.MarkerArea : 0;
                var numberRect = new SKRect(cell.Left, cell.Top, cell.Right, cell.Bottom - marker);
                var markerRect = marker > 0 ? new SKRect(cell.Left, numberRect.Bottom, cell.Right, cell.Bottom) : SKRect.Empty;
                look.DrawCalendarCell(canvas, new SkUiCalendarCellPaint(numberRect, SkUiCalendarCellKind.Day, SelectionShape, selected, part, toLeft, toRight,
                    isToday && TodayStyle != SkUiCalendarTodayStyle.Text, TodayStyle, focused,
                    _pressed is { Part: SkUiCalendarPart.Cell } pressed && pressed.Date == day, dayEnabled && enabled,
                    palette.Selection, palette.Range, palette.Today,
                    style?.BackgroundColor is { } background ? Sk(background, enabled) : SKColors.Transparent, palette.Focus));
                var color = selected ? palette.SelectedText
                    : !dayEnabled ? palette.Disabled
                    : adjacent && part == SkUiCalendarRangePart.Middle ? palette.RangeText
                    : adjacent ? palette.Adjacent
                    : isToday ? palette.TodayText
                    : style?.TextColor is { } custom ? Sk(custom, enabled)
                    : part == SkUiCalendarRangePart.Middle ? palette.RangeText
                    : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? palette.Weekend
                    : palette.Text;
                if (DayPainter is { } painter && painter(canvas, new SkUiCalendarDayContext(this, day, cell, numberRect, markerRect, selected, part,
                        day == today && !adjacent, dayEnabled, adjacent, focused, color, style)))
                    continue;
                var attributes = style?.FontAttributes ?? (day == today && !adjacent ? TodayFontAttributes : FontAttributes);
                DrawText(canvas, Numbers[number], SlotDay + 10 * (int)attributes, WithAttributes(styles.Day, attributes), numberRect, color, TextAlignment.Center,
                    style?.IsStrikethrough == true ? TextDecorations.Strikethrough : TextDecorations.None);
                if (style?.Markers is { Count: > 0 } markers && !markerRect.IsEmpty)
                {
                    if (frame.EventNumber > 0)
                        DrawMarkerLines(canvas, markers, markerRect, numberRect, palette.Text, enabled && dayEnabled && !adjacent, look);
                    else
                        DrawMarkers(canvas, markers, markerRect, numberRect, palette.Text, enabled && dayEnabled && !adjacent, look);
                }
            }
        }
    }

    /// <summary>
    /// Draws a day's markers in one centered row in <paramref name="area"/>: dots, bars (as wide as half the number's shape),
    /// icon glyphs as tall as the area, captions in the look's small size, images (once loaded); what does not fit is left out, a caption shortened.
    /// </summary>
    private void DrawMarkers(SKCanvas canvas, IReadOnlyList<SkUiCalendarMarker> markers, SKRect area, SKRect numberRect, SKColor text, bool bright,
        SkUiLook look, bool smallOnly = false)
    {
        const float gap = 3;
        const float dot = 5;
        const float barHeight = 3;
        var barWidth = Math.Min(numberRect.Width, numberRect.Height) * 0.45f;
        var glyph = area.Height;
        var captionSize = look.CalendarMarkerFontSize;
        // Widths first, to center the row.
        Span<float> widths = stackalloc float[Math.Min(markers.Count, 8)];
        var total = 0f;
        var counted = 0;
        for (var index = 0; index < widths.Length; index++)
        {
            var marker = markers[index];
            // The last line of event lines: only the small marks (dots, glyphs, images) share it.
            if (smallOnly && IsLineMarker(marker))
            {
                widths[index] = 0;
                continue;
            }
            widths[index] = marker.Kind switch
            {
                SkUiCalendarMarkerKind.Dot => dot,
                SkUiCalendarMarkerKind.Bar => barWidth,
                SkUiCalendarMarkerKind.Glyph => glyph,
                SkUiCalendarMarkerKind.Image => marker.Source is null ? 0 : marker.Size > 0 ? (float)marker.Size : glyph,
                _ => marker.Text is { Length: > 0 } caption
                    ? (float)Math.Ceiling(MeasureCustomText(caption, captionSize, FontAttributes.None, marker.FontFamily))
                    : 0
            };
            total += widths[index] + (counted++ > 0 ? gap : 0);
        }
        var x = Math.Max(area.Left + 1, area.MidX - total / 2);
        var paint = _markerPaint ??= new SKPaint { IsAntialias = true };
        for (var index = 0; index < widths.Length && x < area.Right - 1; index++)
        {
            var marker = markers[index];
            if (smallOnly && IsLineMarker(marker))
                continue;
            var width = Math.Min(widths[index], area.Right - 1 - x);
            var color = marker.Color is { } own ? SkUiShapePainter.ToSkColor(own) : text;
            if (!bright)
                color = color.WithAlpha((byte)(color.Alpha / 2));
            switch (marker.Kind)
            {
                case SkUiCalendarMarkerKind.Dot when width >= dot:
                    paint.Color = color;
                    canvas.DrawCircle(x + dot / 2, area.Top + area.Height * 0.4f, dot / 2, paint);
                    break;
                case SkUiCalendarMarkerKind.Bar when width >= barWidth / 2:
                    paint.Color = color;
                    var top = area.Top + area.Height * 0.4f - barHeight / 2;
                    canvas.DrawRoundRect(new SKRect(x, top, x + width, top + barHeight), barHeight / 2, barHeight / 2, paint);
                    break;
                case SkUiCalendarMarkerKind.Glyph when width >= glyph && marker.Text is { Length: > 0 } icon:
                    DrawCustomText(canvas, icon, SKRect.Create(x, area.Top, glyph, area.Height), color, glyph * 0.85, FontAttributes.None, marker.FontFamily,
                        TextAlignment.Center);
                    break;
                case SkUiCalendarMarkerKind.Image when width >= widths[index] && marker.Source is { } source:
                    // Left out (its room kept) until loaded; the load redraws the calendar.
                    if (MarkerImage(source) is { Image: not null } image)
                    {
                        var square = widths[index];
                        var bounds = SKRect.Create(x, area.MidY - square / 2, square, square);
                        var layer = -1;
                        if (!bright)
                        {
                            paint.Color = SKColors.Black.WithAlpha(128);
                            layer = canvas.SaveLayer(bounds, paint);
                        }
                        image.Paint(canvas, bounds, Aspect.AspectFit);
                        if (layer >= 0)
                            canvas.RestoreToCount(layer);
                    }
                    break;
                case SkUiCalendarMarkerKind.Caption when width > 0 && marker.Text is { Length: > 0 } caption:
                    DrawCustomText(canvas, caption, SKRect.Create(x, area.Top, width, area.Height), color, captionSize, FontAttributes.None,
                        marker.FontFamily, TextAlignment.Center);
                    break;
            }
            x += widths[index] + gap;
        }
    }

    /// <summary>Whether a marker takes a line of its own with <see cref="SkUiCalendarDayMarkerLayout.Lines"/> (a caption or a bar).</summary>
    private static bool IsLineMarker(SkUiCalendarMarker marker) =>
        marker.Kind == SkUiCalendarMarkerKind.Bar || marker.Kind == SkUiCalendarMarkerKind.Caption && marker.Text is { Length: > 0 };

    /// <summary>Whether a marker shares the last line with <see cref="SkUiCalendarDayMarkerLayout.Lines"/> (a dot, a glyph, an image).</summary>
    private static bool IsSmallMarker(SkUiCalendarMarker marker) => marker.Kind switch
    {
        SkUiCalendarMarkerKind.Dot => true,
        SkUiCalendarMarkerKind.Glyph => marker.Text is { Length: > 0 },
        SkUiCalendarMarkerKind.Image => marker.Source is not null,
        _ => false
    };

    /// <summary>
    /// Draws a day's markers as event lines in <paramref name="area"/> (<see cref="SkUiCalendarDayMarkerLayout.Lines"/>): a caption as
    /// a chip of its color with its text, a bar as a thin line, one per line in the order given, then the dots, glyphs and images
    /// together on one more line. When they do not all fit, the last line that fits says how many are left out instead.
    /// </summary>
    private void DrawMarkerLines(SKCanvas canvas, IReadOnlyList<SkUiCalendarMarker> markers, SKRect area, SKRect numberRect, SKColor text, bool bright,
        SkUiLook look)
    {
        var lineHeight = (float)MarkerLineHeight(look);
        var lines = (int)Math.Floor((area.Height + 0.5f) / lineHeight);
        if (lines <= 0)
            return;
        int lineMarkers = 0, smallMarkers = 0;
        for (var index = 0; index < markers.Count; index++)
        {
            if (IsLineMarker(markers[index]))
                lineMarkers++;
            else if (IsSmallMarker(markers[index]))
                smallMarkers++;
        }
        var needed = lineMarkers + (smallMarkers > 0 ? 1 : 0);
        var overflow = needed > lines;
        // With too many, the last line says how many are left out: the lines before it show the first events.
        var shownLines = overflow ? lines - 1 : lineMarkers;
        const float inset = 2;
        var paint = _markerPaint ??= new SKPaint { IsAntialias = true };
        var top = area.Top;
        var shown = 0;
        for (var index = 0; index < markers.Count && shown < shownLines; index++)
        {
            var marker = markers[index];
            if (!IsLineMarker(marker))
                continue;
            var color = marker.Color is { } own ? SkUiShapePainter.ToSkColor(own) : text;
            if (!bright)
                color = color.WithAlpha((byte)(color.Alpha / 2));
            var line = new SKRect(area.Left + inset, top + 1, area.Right - inset, top + lineHeight - 1);
            paint.Color = color;
            if (marker.Kind == SkUiCalendarMarkerKind.Bar)
            {
                const float barHeight = 3;
                var barTop = line.MidY - barHeight / 2;
                canvas.DrawRoundRect(new SKRect(line.Left, barTop, line.Right, barTop + barHeight), barHeight / 2, barHeight / 2, paint);
            }
            else
            {
                canvas.DrawRoundRect(line, 3, 3, paint);
                var textColor = ContrastSk(color);
                DrawCustomText(canvas, marker.Text!, new SKRect(line.Left + 3, line.Top, line.Right - 3, line.Bottom), textColor,
                    look.CalendarMarkerFontSize, FontAttributes.None, marker.FontFamily, TextAlignment.Start);
            }
            top += lineHeight;
            shown++;
        }
        var rest = SKRect.Create(area.Left, top, area.Width, lineHeight);
        if (overflow)
        {
            var left = markers.Count - shown;
            var more = MoreEventsText(left);
            DrawCustomText(canvas, more, new SKRect(rest.Left + inset + 3, rest.Top, rest.Right - inset, rest.Bottom),
                bright ? text : text.WithAlpha((byte)(text.Alpha / 2)), look.CalendarMarkerFontSize, FontAttributes.None, null, TextAlignment.Start);
        }
        else if (smallMarkers > 0)
            DrawMarkers(canvas, markers, rest, numberRect, text, bright, look, smallOnly: true);
    }

    // The "+N" texts, made once per count and strings format (a frame allocates none).
    private readonly Dictionary<int, string> _moreEvents = [];
    private string? _moreEventsFormat;

    private string MoreEventsText(int count)
    {
        var format = SkUiStrings.Current.MoreEventsFormat;
        if (!ReferenceEquals(format, _moreEventsFormat))
        {
            _moreEvents.Clear();
            _moreEventsFormat = format;
        }
        if (!_moreEvents.TryGetValue(count, out var text))
            _moreEvents[count] = text = string.Format(Culture, format, count);
        return text;
    }

    /// <summary>White or near black on <paramref name="fill"/> (an event chip's text).</summary>
    private static SKColor ContrastSk(SKColor fill) =>
        fill.Alpha < 128 ? new SKColor(0x1C, 0x1C, 0x1E)
        : 0.299 * fill.Red + 0.587 * fill.Green + 0.114 * fill.Blue > 0.62 * 255 ? new SKColor(0x1C, 0x1C, 0x1E) : SKColors.White;

    /// <summary>A style for the app's text (markers, a day painter's): one line, shrinking a little, then shortened.</summary>
    private SkUiTextStyle CustomStyle(double fontSize, FontAttributes attributes, string? family)
    {
        var size = SkUiFontScaling.ScaleFontSize(double.IsNaN(fontSize) ? SkUiLook.Current.CalendarFontSize : fontSize, FontAutoScalingEnabled);
        return new SkUiTextStyle(SkUiTypefaces.Resolve(family ?? FontFamily, attributes), size, LineBreakMode.TailTruncation, null, -1, -1, 0,
            host.CalendarRightToLeft ? SkUiTextDirection.RightToLeft : SkUiTextDirection.Auto, SkUiTextRendering.Default, attributes, MinimumScale: 0.8f);
    }

    private static int CustomSlot(double fontSize, FontAttributes attributes, string? family) =>
        (HashCode.Combine(fontSize, attributes, family) | 0x40000000) & ~MeasureSlot; // never one of the calendar's own slots

    /// <summary>
    /// Marks the layout a text is measured with (unbounded), apart from the one it is drawn with (in its room): a layout keeps the
    /// lines of one width, so sharing one would lay the text out again twice per frame.
    /// </summary>
    private const int MeasureSlot = 0x20000000;

    private double MeasureCustomText(string text, double fontSize, FontAttributes attributes, string? family) =>
        _texts.Get(text, CustomSlot(fontSize, attributes, family) | MeasureSlot)
            .Measure(text, CustomStyle(fontSize, attributes, family), default, double.PositiveInfinity).Width;

    /// <summary>Draws the app's text (a marker caption or glyph, a day painter's text) in <paramref name="bounds"/>.</summary>
    internal void DrawCustomText(SKCanvas canvas, string text, SKRect bounds, SKColor color, double fontSize, FontAttributes attributes, string? family,
        TextAlignment alignment) =>
        DrawText(canvas, text, CustomSlot(fontSize, attributes, family), CustomStyle(fontSize, attributes, family), bounds, color, alignment);

    private void DrawPeriodPage(SKCanvas canvas, in Frame frame, SkUiCalendarDisplayMode mode, DateTime start, float dx, float dy, in Palette palette,
        in Styles styles, SkUiLook look, CultureInfo culture, bool focusVisible)
    {
        var today = DateTime.Today;
        var enabled = host.CalendarEnabled;
        var todayIndex = SkUiCalendarGrid.PeriodIndex(start, mode, today, _system);
        var focusIndex = focusVisible && _focus is { } focus ? SkUiCalendarGrid.PeriodIndex(start, mode, focus, _system) : -1;
        for (var index = 0; index < SkUiCalendarGrid.PeriodCellCount(mode, _system); index++)
        {
            if (SkUiCalendarGrid.PeriodCell(start, mode, index, _system) is not { } date)
                continue;
            var adjacent = SkUiCalendarGrid.IsAdjacentPeriodCell(mode, index);
            if (adjacent && !AreAdjacentDaysVisible)
                continue;
            var cell = CellRect(frame, month: false, index / SkUiCalendarGrid.PeriodColumns, index % SkUiCalendarGrid.PeriodColumns);
            cell.Offset(dx, dy);
            var cellEnabled = IsPeriodEnabled(date, mode);
            var selected = IsSelectedPeriod(date, mode);
            var isToday = index == todayIndex && TodayStyle != SkUiCalendarTodayStyle.None;
            look.DrawCalendarCell(canvas, new SkUiCalendarCellPaint(cell, SkUiCalendarCellKind.Period, SelectionShape, selected, SkUiCalendarRangePart.None,
                false, false, isToday && TodayStyle != SkUiCalendarTodayStyle.Text, TodayStyle, index == focusIndex,
                _pressed is { Part: SkUiCalendarPart.Cell } pressed && pressed.Index == index && mode == _mode, cellEnabled && enabled,
                palette.Selection, palette.Range, palette.Today, SKColors.Transparent, palette.Focus));
            var color = selected ? palette.SelectedText
                : !cellEnabled ? palette.Disabled
                : adjacent ? palette.Adjacent
                : isToday ? palette.TodayText
                : palette.Text;
            var attributes = isToday ? TodayFontAttributes : FontAttributes;
            DrawText(canvas, SkUiCalendarGrid.PeriodText(date, mode, culture, _system), SlotDay + 10 * (int)attributes, WithAttributes(styles.Day, attributes), cell,
                color, TextAlignment.Center);
        }
    }

    private void DrawText(SKCanvas canvas, string text, int slot, in SkUiTextStyle style, SKRect rect, SKColor color, TextAlignment horizontal,
        TextDecorations decorations = TextDecorations.None)
    {
        if (text.Length == 0 || rect.Width <= 0 || rect.Height <= 0 || color.Alpha == 0)
            return;
        var paint = SkUiTextResources.TextPaint;
        paint.Color = color;
        var save = canvas.Save();
        canvas.Translate(rect.Left, rect.Top);
        // Cells of a column grid differ in width by float rounding (10^-5 DIP from column to column): a slot handed to the layout
        // as it is would miss its fit cache in every cell, so a text that shrinks or shortens to fit (a marker caption) would be
        // fitted and laid out again for each day of each frame. Snapped to 1/16 DIP, every column gives the same slot.
        var width = MathF.Round(rect.Width * SlotSnap) / SlotSnap;
        var height = MathF.Round(rect.Height * SlotSnap) / SlotSnap;
        _texts.Get(text, slot).Draw(canvas, text, style, default, width, height, horizontal, TextAlignment.Center, paint, decorations);
        canvas.RestoreToCount(save);
    }

    /// <summary>The fraction of a DIP text slots are snapped to (<see cref="DrawText"/>).</summary>
    private const float SlotSnap = 16;

    /// <summary>Disposes the text blobs and the layer paint (the node's pictures are gone); they are made again when drawn.</summary>
    public void Release()
    {
        _texts.Release();
        // The image markers' cache entries go back to the cache; they are loaded again (from memory) when drawn.
        foreach (var entry in _markerImages.Values)
            entry.Slot.Dispose();
        _markerImages.Clear();
        _layerPaint?.Dispose();
        _layerPaint = null;
        _markerPaint?.Dispose();
        _markerPaint = null;
    }

    /// <summary>Stops the running animations where they end (the node left its surface).</summary>
    public void StopAnimations()
    {
        StopSlide();
        _zoom?.Jump(1);
    }

    #endregion
}

/// <summary>
/// The text layouts a calendar draws with, one per text and style slot, so each keeps its lines: day numbers, day names and
/// month names repeat on every page and are laid out once. Bounded: past the limit (many years shown) it starts over.
/// </summary>
internal sealed class SkUiCalendarTexts
{
    private const int Limit = 256;
    private readonly Dictionary<(string Text, int Slot), SkUiTextLayout> _layouts = [];

    public int Count => _layouts.Count;

    /// <summary>The texts kept (tests).</summary>
    public IEnumerable<string> Texts => _layouts.Keys.Select(key => key.Text);

    public SkUiTextLayout Get(string text, int slot)
    {
        if (_layouts.TryGetValue((text, slot), out var layout))
            return layout;
        if (_layouts.Count >= Limit)
            Release();
        layout = new SkUiTextLayout();
        _layouts[(text, slot)] = layout;
        return layout;
    }

    public void Release()
    {
        foreach (var layout in _layouts.Values)
            layout.Release();
        _layouts.Clear();
    }
}

/// <summary>
/// A calendar's taps and swipes: a tap chooses what is under it (claimed on release, so a scroller around the calendar can
/// take a drag first); a drag along the navigation axis past the touch slop moves the page, and one across it is left to
/// scrollers. Waits while a pinch has two fingers down.
/// </summary>
internal sealed class SkUiCalendarGestureRecognizer(SkUiCalendarModel model, Func<Size> size, Func<bool> waitsForPinch) : SkUiGestureRecognizer
{
    private long? _pointer;
    private Point _start;
    private bool _active;
    private SkUiVelocityTracker _velocity;

    /// <inheritdoc />
    protected internal override bool IsExclusive => true;

    /// <inheritdoc />
    protected internal override bool IsDiscrete => true;

    /// <inheritdoc />
    protected internal override bool OnPointerPressed(SkUiPointer pointer)
    {
        if (_pointer is not null)
            return false;
        _pointer = pointer.Id;
        _start = pointer.Position;
        _active = false;
        _velocity.Reset(pointer.Timestamp, pointer.Position);
        var (width, height) = Size();
        model.Press(Local(pointer), width, height);
        return true;
    }

    /// <inheritdoc />
    protected internal override void OnPointerMoved(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        var (dx, dy) = (pointer.Position.X - _start.X, pointer.Position.Y - _start.Y);
        var horizontal = model.NavigationOrientation == StackOrientation.Horizontal;
        var (width, height) = Size();
        if (_active)
        {
            model.Drag(horizontal ? dx : dy, width, height);
            return;
        }
        if (ExceedsSlop(dx, dy, horizontal ? SkUiPanAxis.Horizontal : SkUiPanAxis.Vertical))
        {
            if (!model.CanSwipe || waitsForPinch())
            {
                model.Unpress();
                return;
            }
            Claim();
            if (_pointer != pointer.Id)
                return;
            _active = true;
            model.BeginDrag();
            model.Drag(horizontal ? dx : dy, width, height);
        }
        else if (ExceedsSlop(dx, dy, horizontal ? SkUiPanAxis.Vertical : SkUiPanAxis.Horizontal))
        {
            model.Unpress();
            Resign(); // a drag across the navigation axis belongs to scrollers around the calendar
        }
    }

    /// <inheritdoc />
    protected internal override void OnPointerReleased(SkUiPointer pointer)
    {
        if (_pointer != pointer.Id)
            return;
        _velocity.Add(pointer.Timestamp, pointer.Position);
        _pointer = null;
        var (width, height) = Size();
        if (_active)
        {
            _active = false;
            var velocity = _velocity.Velocity;
            model.EndDrag(model.NavigationOrientation == StackOrientation.Horizontal ? velocity.X : velocity.Y, width, height);
            return;
        }
        var local = Local(pointer);
        model.Unpress();
        if (IsInsideOwner(local) && !waitsForPinch())
        {
            Claim();
            model.Tap(local, width, height);
        }
        else
            Resign();
    }

    /// <inheritdoc />
    protected internal override void OnRejected(long pointerId)
    {
        if (_pointer != pointerId)
            return;
        _pointer = null;
        model.Unpress();
        if (_active)
        {
            _active = false;
            model.CancelDrag();
        }
    }

    private (float Width, float Height) Size()
    {
        var value = size();
        return ((float)value.Width, (float)value.Height);
    }

    private Point Local(SkUiPointer pointer) => Owner is { } owner ? pointer.GetPosition(owner) : pointer.Position;
}
