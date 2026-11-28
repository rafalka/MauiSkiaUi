using System.Collections.ObjectModel;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A drawn calendar: a month of days, or the months of a year, the years of a decade, the decades of a century, with a
/// header, day names, today, a selection (one day, several, or a range) and the date picker's day rules
/// (<see cref="MinimumDate"/>, <see cref="MaximumDate"/>, <see cref="BlackoutDates"/>, <see cref="DateEnabled"/>). One
/// drawn view, no view per day: the look draws the cells (<see cref="SkUiLook.DrawCalendarCell"/>), the calendar their
/// texts. MAUI has no calendar; the API is SkiaUi's own. Core twin: <see cref="Core.SkUiCoreCalendar"/>.
/// </summary>
/// <remarks>
/// <para>
/// Swipe or use the header's buttons to move between pages (<see cref="GoToPrevious"/>, <see cref="GoToNext"/>,
/// <see cref="GoToDate"/>); tap the title to step up a display mode, a month or year to step down, or pinch
/// (<see cref="MinimumDisplayMode"/> and <see cref="MaximumDisplayMode"/> limit the modes). <see cref="DisplayDate"/>,
/// <see cref="DisplayMode"/>, <see cref="SelectedDate"/>, <see cref="SelectedDates"/> and <see cref="SelectedRange"/>
/// are two-way: the user's changes are written back without cutting a one-way binding.
/// </para>
/// <para>
/// Every color is <c>null</c> by default: the color scheme's color when drawn, so the calendar follows a dark-mode switch.
/// The keyboard moves a focus over the days (arrows, Page Up / Down, Home / End, Enter / Space); screen readers get one
/// adjustable element whose value is the focused day.
/// </para>
/// </remarks>
public class SkUiCalendar : SkUiView, ISkUiCalendarHost, ISkUiKeyHandler, ISkUiFontElement
{
    private SkUiCalendarModel? _model;
    /// <summary>Created on first use: property callbacks can run before the constructor (see <see cref="SkUiView.StyledOr{T}"/>).</summary>
    private SkUiCalendarModel Model => _model ??= new SkUiCalendarModel(this);

    /// <summary>The day the keyboard acts on, or <c>null</c> (a date picker's calendar popup).</summary>
    internal DateTime? FocusDate => Model.FocusDate;
    private SkUiWeakListener<SkUiCalendar>? _blackoutListener;
    private SkUiWeakListener<SkUiCalendar> BlackoutListener =>
        _blackoutListener ??= new SkUiWeakListener<SkUiCalendar>(this, static (calendar, _) => calendar.OnBlackoutDatesChanged());
    private SkUiCalendarGestureRecognizer? _gesture;
    private SkUiPinchGestureRecognizer? _pinch;
    // The control writes the engine's state back to its own properties: their callbacks then have nothing to do.
    private bool _syncing;
    private string? _reportedTitle;

    /// <summary>Creates a calendar showing this month, with single selection.</summary>
    public SkUiCalendar() => SkUiStrings.CurrentChanged += OnStringsChanged;

    /// <summary>The app's texts changed (day names): redraw. An instance method, so the weak event keeps no calendar alive.</summary>
    private void OnStringsChanged(object? sender, EventArgs e)
    {
        InvalidatePaint();
        InvalidateSemantics();
    }

    #region Events and methods

    /// <summary>Raised when the selection changes (the user's change or the app's), with the days added and removed or the range before and after.</summary>
    public event EventHandler<SkUiCalendarSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Raised when the user taps a cell (also a disabled day, with <see cref="SkUiCalendarDateTappedEventArgs.IsEnabled"/> <c>false</c>).</summary>
    public event EventHandler<SkUiCalendarDateTappedEventArgs>? DateTapped;

    /// <summary>
    /// Raised when the user taps a week number (<see cref="AreWeekNumbersVisible"/>), with the week's days: select the whole
    /// week with <c>SelectedRange = e.EnabledRange</c> (range selection) or <c>SelectedDates</c> (multiple selection).
    /// </summary>
    public event EventHandler<SkUiCalendarWeekNumberTappedEventArgs>? WeekNumberTapped;

    /// <summary>Whether the page can move back (not before <see cref="MinimumDate"/>'s page).</summary>
    public bool CanGoToPrevious => Model.CanStep(-1);

    /// <summary>Whether the page can move on (not past <see cref="MaximumDate"/>'s page).</summary>
    public bool CanGoToNext => Model.CanStep(1);

    /// <summary>The header's text for the page shown ("November 2026", "2026", "2020 – 2029"), also when the header is hidden: for a header of the app's own.</summary>
    public string Title => Model.Title;

    /// <summary>Shows the previous month (or year, decade, century), sliding when <paramref name="animated"/>; returns whether it moved.</summary>
    public bool GoToPrevious(bool animated = true) => Model.Step(-1, animated);

    /// <summary>Shows the next month (or year, decade, century), sliding when <paramref name="animated"/>; returns whether it moved.</summary>
    public bool GoToNext(bool animated = true) => Model.Step(1, animated);

    /// <summary>Shows <paramref name="date"/>'s month (in the lowest display mode), sliding when <paramref name="animated"/> and it is another page.</summary>
    public void GoToDate(DateTime date, bool animated = true) => Model.GoToDate(date, animated);

    /// <summary>Shows today's month.</summary>
    public void GoToToday(bool animated = true) => Model.GoToDate(DateTime.Today, animated);

    /// <summary>Clears the selection of every mode.</summary>
    public void ClearSelection()
    {
        if (Model.Clear() is { } args)
            SelectionDone(args);
    }

    /// <summary>Asks <see cref="DayStyle"/> again for every day (the app's data behind it changed).</summary>
    public void RefreshDayStyles()
    {
        Model.RefreshDayStyles();
        InvalidatePaint();
    }

    #endregion

    #region Display

    /// <summary>Bindable property for <see cref="DisplayDate"/> (two-way by default: the user moves the page).</summary>
    public static readonly BindableProperty DisplayDateProperty = BindableProperty.Create(nameof(DisplayDate), typeof(DateTime), typeof(SkUiCalendar), default(DateTime),
        BindingMode.TwoWay,
        coerceValue: (view, value) => ((SkUiCalendar)view).Model.ClampToRange((DateTime)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnDisplayDateChanged((DateTime)value),
        defaultValueCreator: _ => DateTime.Today);

    /// <summary>A date of the page shown (default today; its time is dropped). Kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>; written back when the user moves the page.</summary>
    public DateTime DisplayDate { get => (DateTime)GetValue(DisplayDateProperty); set => SetValue(DisplayDateProperty, value); }

    /// <summary>Sets <see cref="DisplayDate"/> (no slide; <see cref="GoToDate"/> slides).</summary>
    public SkUiCalendar SetDisplayDate(DateTime value) { DisplayDate = value; return this; }

    /// <summary>Bindable property for <see cref="DisplayMode"/> (two-way by default: the user changes it).</summary>
    public static readonly BindableProperty DisplayModeProperty = BindableProperty.Create(nameof(DisplayMode), typeof(SkUiCalendarDisplayMode), typeof(SkUiCalendar),
        SkUiCalendarDisplayMode.Month, BindingMode.TwoWay,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarDisplayMode)value),
        coerceValue: (view, value) => ((SkUiCalendar)view).Model.ClampMode((SkUiCalendarDisplayMode)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnDisplayModeChanged((SkUiCalendarDisplayMode)value));

    /// <summary>What the page shows: days (default), months, years or decades; kept within <see cref="MinimumDisplayMode"/> and <see cref="MaximumDisplayMode"/>. Written back when the user changes it.</summary>
    public SkUiCalendarDisplayMode DisplayMode { get => (SkUiCalendarDisplayMode)GetValue(DisplayModeProperty); set => SetValue(DisplayModeProperty, value); }

    /// <summary>Sets <see cref="DisplayMode"/> (no zoom).</summary>
    public SkUiCalendar SetDisplayMode(SkUiCalendarDisplayMode value) { ThrowIfUndefined(value); DisplayMode = value; return this; }

    /// <summary>Bindable property for <see cref="MinimumDisplayMode"/>.</summary>
    public static readonly BindableProperty MinimumDisplayModeProperty = BindableProperty.Create(nameof(MinimumDisplayMode), typeof(SkUiCalendarDisplayMode), typeof(SkUiCalendar),
        SkUiCalendarDisplayMode.Month,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarDisplayMode)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.MinimumDisplayMode = (SkUiCalendarDisplayMode)value, measure: false));

    /// <summary>The lowest display mode (default <see cref="SkUiCalendarDisplayMode.Month"/>); <see cref="SkUiCalendarDisplayMode.Year"/> makes a month picker, whose month cells are chosen.</summary>
    public SkUiCalendarDisplayMode MinimumDisplayMode { get => (SkUiCalendarDisplayMode)GetValue(MinimumDisplayModeProperty); set => SetValue(MinimumDisplayModeProperty, value); }

    /// <summary>Sets <see cref="MinimumDisplayMode"/>.</summary>
    public SkUiCalendar SetMinimumDisplayMode(SkUiCalendarDisplayMode value) { ThrowIfUndefined(value); MinimumDisplayMode = value; return this; }

    /// <summary>Bindable property for <see cref="MaximumDisplayMode"/>.</summary>
    public static readonly BindableProperty MaximumDisplayModeProperty = BindableProperty.Create(nameof(MaximumDisplayMode), typeof(SkUiCalendarDisplayMode), typeof(SkUiCalendar),
        SkUiCalendarDisplayMode.Century,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarDisplayMode)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.MaximumDisplayMode = (SkUiCalendarDisplayMode)value, measure: false));

    /// <summary>The highest display mode (default <see cref="SkUiCalendarDisplayMode.Century"/>); <see cref="SkUiCalendarDisplayMode.Month"/> keeps the calendar on days.</summary>
    public SkUiCalendarDisplayMode MaximumDisplayMode { get => (SkUiCalendarDisplayMode)GetValue(MaximumDisplayModeProperty); set => SetValue(MaximumDisplayModeProperty, value); }

    /// <summary>Sets <see cref="MaximumDisplayMode"/>.</summary>
    public SkUiCalendar SetMaximumDisplayMode(SkUiCalendarDisplayMode value) { ThrowIfUndefined(value); MaximumDisplayMode = value; return this; }

    /// <summary>Bindable property for <see cref="VisibleWeeks"/>.</summary>
    public static readonly BindableProperty VisibleWeeksProperty = BindableProperty.Create(nameof(VisibleWeeks), typeof(int), typeof(SkUiCalendar), SkUiCalendarGrid.MaxWeeks,
        validateValue: (_, value) => value is int weeks and >= 1 and <= SkUiCalendarGrid.MaxWeeks,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.VisibleWeeks = (int)value, measure: true));

    /// <summary>How many weeks a month page shows (1 to 6, default 6: the whole month); fewer pages by weeks (a weekly strip).</summary>
    public int VisibleWeeks { get => (int)GetValue(VisibleWeeksProperty); set => SetValue(VisibleWeeksProperty, value); }

    /// <summary>Sets <see cref="VisibleWeeks"/>.</summary>
    public SkUiCalendar SetVisibleWeeks(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiCalendarGrid.MaxWeeks);
        VisibleWeeks = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="FirstDayOfWeek"/>.</summary>
    public static readonly BindableProperty FirstDayOfWeekProperty = BindableProperty.Create(nameof(FirstDayOfWeek), typeof(DayOfWeek?), typeof(SkUiCalendar), null,
        validateValue: (_, value) => value is null || Enum.IsDefined((DayOfWeek)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.FirstDayOfWeek = (DayOfWeek?)value, measure: false));

    /// <summary>Bindable property for <see cref="CalendarSystem"/>.</summary>
    public static readonly BindableProperty CalendarSystemProperty = BindableProperty.Create(nameof(CalendarSystem), typeof(System.Globalization.Calendar),
        typeof(SkUiCalendar), null, validateValue: (_, value) => SkUiCalendarSystem.IsSupported((System.Globalization.Calendar?)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.CalendarSystem = (System.Globalization.Calendar?)value, measure: false));

    /// <summary>
    /// The calendar the pages are cut by (1.15): <c>null</c> (default) or a <see cref="System.Globalization.GregorianCalendar"/>,
    /// the Gregorian one; a <see cref="System.Globalization.ThaiBuddhistCalendar"/>, <see cref="System.Globalization.TaiwanCalendar"/>,
    /// <see cref="System.Globalization.KoreanCalendar"/> or <see cref="System.Globalization.JapaneseCalendar"/> keeps the Gregorian
    /// months and numbers years its way; a <see cref="System.Globalization.UmAlQuraCalendar"/>, <see cref="System.Globalization.HijriCalendar"/>,
    /// <see cref="System.Globalization.PersianCalendar"/> or <see cref="System.Globalization.HebrewCalendar"/> cuts the pages by its
    /// own months (13 in a Hebrew leap year). Dates stay <see cref="DateTime"/> values: selection, rules and day styles do not change
    /// with the calendar shown. The pages stop at the days the calendar supports. Titles, month names and day names come from the
    /// current culture when it can write the calendar, else from the calendar's own culture. Other calendars are ignored (SkUi layer,
    /// with a warning) or rejected (fluent setter).
    /// </summary>
    public System.Globalization.Calendar? CalendarSystem
    {
        get => (System.Globalization.Calendar?)GetValue(CalendarSystemProperty);
        set => SetValue(CalendarSystemProperty, value);
    }

    /// <summary>Sets <see cref="CalendarSystem"/>; throws for a calendar the pages cannot be cut by.</summary>
    public SkUiCalendar SetCalendarSystem(System.Globalization.Calendar? value)
    {
        if (!SkUiCalendarSystem.IsSupported(value))
            throw new ArgumentOutOfRangeException(nameof(value), value?.GetType().Name, "The calendar's pages cannot be cut by this calendar.");
        CalendarSystem = value;
        return this;
    }

    /// <summary>The week's first day, its first column; <c>null</c> (default): the current culture's.</summary>
    public DayOfWeek? FirstDayOfWeek { get => (DayOfWeek?)GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>Sets <see cref="FirstDayOfWeek"/>.</summary>
    public SkUiCalendar SetFirstDayOfWeek(DayOfWeek? value)
    {
        if (value is { } day && !Enum.IsDefined(day))
            throw new ArgumentOutOfRangeException(nameof(value));
        FirstDayOfWeek = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TitleFormat"/>.</summary>
    public static readonly BindableProperty TitleFormatProperty = BindableProperty.Create(nameof(TitleFormat), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Revalidated(model => model.TitleFormat = (string?)value, measure: false));

    /// <summary>The title's .NET date format on month pages (<c>"MMM yyyy"</c>); <c>null</c> (default): the culture's year-month pattern.</summary>
    public string? TitleFormat { get => (string?)GetValue(TitleFormatProperty); set => SetValue(TitleFormatProperty, value); }

    /// <summary>Sets <see cref="TitleFormat"/>.</summary>
    public SkUiCalendar SetTitleFormat(string? value) { TitleFormat = value; return this; }

    #endregion

    #region Selection and rules

    /// <summary>Bindable property for <see cref="SelectionMode"/>.</summary>
    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(nameof(SelectionMode), typeof(SkUiCalendarSelectionMode), typeof(SkUiCalendar),
        SkUiCalendarSelectionMode.Single,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarSelectionMode)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.SelectionMode = (SkUiCalendarSelectionMode)value));

    /// <summary>How many days the user can choose (default one).</summary>
    public SkUiCalendarSelectionMode SelectionMode { get => (SkUiCalendarSelectionMode)GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>Sets <see cref="SelectionMode"/>.</summary>
    public SkUiCalendar SetSelectionMode(SkUiCalendarSelectionMode value) { ThrowIfUndefined(value); SelectionMode = value; return this; }

    /// <summary>Bindable property for <see cref="RangeSelectionTarget"/> (two-way by default: a tap on the start moves it to the end).</summary>
    public static readonly BindableProperty RangeSelectionTargetProperty = BindableProperty.Create(nameof(RangeSelectionTarget), typeof(SkUiCalendarRangeTarget),
        typeof(SkUiCalendar), SkUiCalendarRangeTarget.Auto, BindingMode.TwoWay,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarRangeTarget)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Applied(model => model.RangeSelectionTarget = (SkUiCalendarRangeTarget)value));

    /// <summary>
    /// Which end of the range a tap sets (default <see cref="SkUiCalendarRangeTarget.Auto"/>: the first tap starts a range, the
    /// second ends it). <see cref="SkUiCalendarRangeTarget.Start"/> / <see cref="SkUiCalendarRangeTarget.End"/> let the app (a
    /// "start" and an "end" field above the calendar) say which; after a tap on the start the calendar moves it to the end
    /// (written back).
    /// </summary>
    public SkUiCalendarRangeTarget RangeSelectionTarget { get => (SkUiCalendarRangeTarget)GetValue(RangeSelectionTargetProperty); set => SetValue(RangeSelectionTargetProperty, value); }

    /// <summary>Sets <see cref="RangeSelectionTarget"/>.</summary>
    public SkUiCalendar SetRangeSelectionTarget(SkUiCalendarRangeTarget value) { ThrowIfUndefined(value); RangeSelectionTarget = value; return this; }

    /// <summary>Bindable property for <see cref="DayMarkerAreaHeight"/>.</summary>
    public static readonly BindableProperty DayMarkerAreaHeightProperty = BindableProperty.Create(nameof(DayMarkerAreaHeight), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.DayMarkerAreaHeight = (double)value));

    /// <summary>
    /// The height of the area under each day's number for its markers (or a day painter's use); negative (default -1): the
    /// look's (<see cref="SkUiLook.CalendarMarkerAreaHeight"/>) when <see cref="DayStyle"/> or <see cref="DayPainter"/> is set,
    /// else none. Rows grow by it when the calendar sizes itself.
    /// </summary>
    public double DayMarkerAreaHeight { get => (double)GetValue(DayMarkerAreaHeightProperty); set => SetValue(DayMarkerAreaHeightProperty, value); }

    /// <summary>Sets <see cref="DayMarkerAreaHeight"/>.</summary>
    public SkUiCalendar SetDayMarkerAreaHeight(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); DayMarkerAreaHeight = value; return this; }

    /// <summary>Bindable property for <see cref="DayMarkerLayout"/>.</summary>
    public static readonly BindableProperty DayMarkerLayoutProperty = BindableProperty.Create(nameof(DayMarkerLayout), typeof(SkUiCalendarDayMarkerLayout),
        typeof(SkUiCalendar), SkUiCalendarDayMarkerLayout.Row, validateValue: (_, value) => Enum.IsDefined((SkUiCalendarDayMarkerLayout)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.DayMarkerLayout = (SkUiCalendarDayMarkerLayout)value));

    /// <summary>
    /// How a day's markers are laid out (1.15): <see cref="SkUiCalendarDayMarkerLayout.Row"/> (default) in one row under the
    /// number, or <see cref="SkUiCalendarDayMarkerLayout.Lines"/>, a month view with events: the number at the top of the day
    /// (the look's <see cref="SkUiLook.CalendarEventNumberHeight"/>) and the markers one per line under it, as many as the row's
    /// height holds (give the calendar the height of the page). Sized by itself, a row holds three lines, or
    /// <see cref="DayMarkerAreaHeight"/> when set.
    /// </summary>
    public SkUiCalendarDayMarkerLayout DayMarkerLayout
    {
        get => (SkUiCalendarDayMarkerLayout)GetValue(DayMarkerLayoutProperty);
        set => SetValue(DayMarkerLayoutProperty, value);
    }

    /// <summary>Sets <see cref="DayMarkerLayout"/>.</summary>
    public SkUiCalendar SetDayMarkerLayout(SkUiCalendarDayMarkerLayout value)
    {
        ThrowIfUndefined(value);
        DayMarkerLayout = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayMarkerLineHeight"/>.</summary>
    public static readonly BindableProperty DayMarkerLineHeightProperty = BindableProperty.Create(nameof(DayMarkerLineHeight), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.DayMarkerLineHeight = (double)value));

    /// <summary>
    /// The height of one event line with <see cref="DayMarkerLayout"/> <see cref="SkUiCalendarDayMarkerLayout.Lines"/> (1.15);
    /// negative (default -1): the look's (<see cref="SkUiLook.CalendarMarkerLineHeight"/>, 16).
    /// </summary>
    public double DayMarkerLineHeight { get => (double)GetValue(DayMarkerLineHeightProperty); set => SetValue(DayMarkerLineHeightProperty, value); }

    /// <summary>Sets <see cref="DayMarkerLineHeight"/>.</summary>
    public SkUiCalendar SetDayMarkerLineHeight(double value) { SkUiValidate.ThrowIfNotFinite(value, nameof(value)); DayMarkerLineHeight = value; return this; }

    /// <summary>
    /// Draws each day the app's way (events, avatars, prices), after the look's selection and today: the painter gets the
    /// day's parts, state and colors and can draw text through the calendar. Returning <c>true</c> skips the calendar's number
    /// and markers for that day. <c>null</c> (default): none. Not bindable.
    /// </summary>
    public SkUiCalendarDayPainter? DayPainter
    {
        get => Model.DayPainter;
        set
        {
            if (ReferenceEquals(Model.DayPainter, value))
                return;
            OnPropertyChanging();
            Model.DayPainter = value;
            OnPropertyChanged();
            InvalidateMeasureOverride();
            InvalidatePaint();
        }
    }

    /// <summary>Sets <see cref="DayPainter"/>.</summary>
    public SkUiCalendar SetDayPainter(SkUiCalendarDayPainter? value) { DayPainter = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedDate"/> (two-way by default: the user chooses).</summary>
    public static readonly BindableProperty SelectedDateProperty = BindableProperty.Create(nameof(SelectedDate), typeof(DateTime?), typeof(SkUiCalendar), null,
        BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnSelectedDateChanged((DateTime?)value));

    /// <summary>The chosen day (single selection), or the last day chosen (multiple selection, a range's first day); <c>null</c>: none. Written back when the user chooses.</summary>
    public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <summary>Sets <see cref="SelectedDate"/> (its time is dropped).</summary>
    public SkUiCalendar SetSelectedDate(DateTime? value) { SelectedDate = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedDates"/> (two-way by default: the user chooses).</summary>
    public static readonly BindableProperty SelectedDatesProperty = BindableProperty.Create(nameof(SelectedDates), typeof(IReadOnlyList<DateTime>), typeof(SkUiCalendar),
        Array.Empty<DateTime>(), BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnSelectedDatesChanged((IReadOnlyList<DateTime>?)value));

    /// <summary>
    /// The chosen days of multiple selection, in date order; empty by default. The user's changes write a new list (a binding
    /// sees each change); a list the app sets is read when set, not followed.
    /// </summary>
    public IReadOnlyList<DateTime> SelectedDates { get => (IReadOnlyList<DateTime>)GetValue(SelectedDatesProperty); set => SetValue(SelectedDatesProperty, value); }

    /// <summary>Sets <see cref="SelectedDates"/> (times dropped, duplicates removed).</summary>
    public SkUiCalendar SetSelectedDates(IReadOnlyList<DateTime> value) { ArgumentNullException.ThrowIfNull(value); SelectedDates = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedRange"/> (two-way by default: the user chooses).</summary>
    public static readonly BindableProperty SelectedRangeProperty = BindableProperty.Create(nameof(SelectedRange), typeof(SkUiDateRange?), typeof(SkUiCalendar), null,
        BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnSelectedRangeChanged((SkUiDateRange?)value));

    /// <summary>The chosen range of range selection; <c>null</c>: none. After the first tap it is open (its <see cref="SkUiDateRange.End"/> <c>null</c>), after the second complete. Written back when the user chooses.</summary>
    public SkUiDateRange? SelectedRange { get => (SkUiDateRange?)GetValue(SelectedRangeProperty); set => SetValue(SelectedRangeProperty, value); }

    /// <summary>Sets <see cref="SelectedRange"/>.</summary>
    public SkUiCalendar SetSelectedRange(SkUiDateRange? value) { SelectedRange = value; return this; }

    /// <summary>Bindable property for <see cref="SelectedRanges"/> (two-way by default: the user chooses).</summary>
    public static readonly BindableProperty SelectedRangesProperty = BindableProperty.Create(nameof(SelectedRanges), typeof(IReadOnlyList<SkUiDateRange>),
        typeof(SkUiCalendar), Array.Empty<SkUiDateRange>(), BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnSelectedRangesChanged((IReadOnlyList<SkUiDateRange>?)value));

    /// <summary>
    /// The chosen ranges of multiple-range selection, in date order (ranges that overlap or touch joined; while the user chooses
    /// one, it is open); empty by default. The user's changes write a new list; a list the app sets is read when set, not followed.
    /// </summary>
    public IReadOnlyList<SkUiDateRange> SelectedRanges
    {
        get => (IReadOnlyList<SkUiDateRange>)GetValue(SelectedRangesProperty);
        set => SetValue(SelectedRangesProperty, value);
    }

    /// <summary>Sets <see cref="SelectedRanges"/>.</summary>
    public SkUiCalendar SetSelectedRanges(IReadOnlyList<SkUiDateRange> value) { ArgumentNullException.ThrowIfNull(value); SelectedRanges = value; return this; }

    /// <summary>Bindable property for <see cref="MinimumDate"/>.</summary>
    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime?), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnRangeChanged(model => model.MinimumDate = ((DateTime?)value)?.Date));

    /// <summary>The earliest day that can be chosen and shown; <c>null</c> (default): none.</summary>
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }

    /// <summary>Sets <see cref="MinimumDate"/>.</summary>
    public SkUiCalendar SetMinimumDate(DateTime? value) { MinimumDate = value; return this; }

    /// <summary>Bindable property for <see cref="MaximumDate"/>.</summary>
    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime?), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnRangeChanged(model => model.MaximumDate = ((DateTime?)value)?.Date));

    /// <summary>The latest day that can be chosen and shown; <c>null</c> (default): none.</summary>
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    /// <summary>Sets <see cref="MaximumDate"/>.</summary>
    public SkUiCalendar SetMaximumDate(DateTime? value) { MaximumDate = value; return this; }

    /// <summary>Bindable property for <see cref="BlackoutDates"/>.</summary>
    public static readonly BindableProperty BlackoutDatesProperty = BindableProperty.Create(nameof(BlackoutDates), typeof(IList<DateTime>), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).OnBlackoutDatesSet((IList<DateTime>?)value),
        defaultValueCreator: view => ((SkUiCalendar)view).CreateBlackoutDates());

    /// <summary>Days that cannot be chosen (holidays, booked days), dimmed. An empty list by default: add to it, or set your own (an observable list is followed).</summary>
    public IList<DateTime>? BlackoutDates { get => (IList<DateTime>?)GetValue(BlackoutDatesProperty); set => SetValue(BlackoutDatesProperty, value); }

    /// <summary>Sets <see cref="BlackoutDates"/>.</summary>
    public SkUiCalendar SetBlackoutDates(IList<DateTime>? value) { BlackoutDates = value; return this; }

    /// <summary>Whether a day can be chosen, a rule of the app's (weekdays only: <c>date =&gt; date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)</c>); <c>null</c> (default): every day in the range. Not bindable.</summary>
    public Func<DateTime, bool>? DateEnabled
    {
        get => Model.DateEnabled;
        set
        {
            if (ReferenceEquals(Model.DateEnabled, value))
                return;
            OnPropertyChanging();
            Model.DateEnabled = value;
            OnPropertyChanged();
            InvalidatePaint();
        }
    }

    /// <summary>Sets <see cref="DateEnabled"/>.</summary>
    public SkUiCalendar SetDateEnabled(Func<DateTime, bool>? value) { DateEnabled = value; return this; }

    /// <summary>
    /// The app's style per day (holidays, events as marker dots, cancelled days struck through); <c>null</c> (default): none.
    /// Asked once per day while drawing and remembered: call <see cref="RefreshDayStyles"/> when the data behind it changes. Not bindable.
    /// </summary>
    public Func<DateTime, SkUiCalendarDayStyle?>? DayStyle
    {
        get => Model.DayStyle;
        set
        {
            if (ReferenceEquals(Model.DayStyle, value))
                return;
            OnPropertyChanging();
            Model.DayStyle = value;
            OnPropertyChanged();
            InvalidateMeasureOverride(); // the marker area comes and goes with it
            InvalidatePaint();
        }
    }

    /// <summary>Sets <see cref="DayStyle"/>.</summary>
    public SkUiCalendar SetDayStyle(Func<DateTime, SkUiCalendarDayStyle?>? value) { DayStyle = value; return this; }

    #endregion

    #region Appearance and behavior

    /// <summary>Bindable property for <see cref="NavigationOrientation"/>.</summary>
    public static readonly BindableProperty NavigationOrientationProperty = BindableProperty.Create(nameof(NavigationOrientation), typeof(StackOrientation), typeof(SkUiCalendar), StackOrientation.Horizontal,
        validateValue: SkUiValidate.Orientation,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationOrientation = (StackOrientation)value));

    /// <summary>The direction a swipe moves between pages: horizontal (default) or vertical (the header buttons' chevrons then point up and down).</summary>
    public StackOrientation NavigationOrientation { get => (StackOrientation)GetValue(NavigationOrientationProperty); set => SetValue(NavigationOrientationProperty, value); }

    /// <summary>Sets <see cref="NavigationOrientation"/>.</summary>
    public SkUiCalendar SetNavigationOrientation(StackOrientation value)
    {
        SkUiValidate.ThrowIfNotOrientation(value, nameof(value));
        NavigationOrientation = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="IsSwipeEnabled"/>.</summary>
    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(nameof(IsSwipeEnabled), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Applied(model => model.IsSwipeEnabled = (bool)value));

    /// <summary>Whether a swipe moves between pages (default <c>true</c>).</summary>
    public bool IsSwipeEnabled { get => (bool)GetValue(IsSwipeEnabledProperty); set => SetValue(IsSwipeEnabledProperty, value); }

    /// <summary>Sets <see cref="IsSwipeEnabled"/>.</summary>
    public SkUiCalendar SetIsSwipeEnabled(bool value)
    {
        IsSwipeEnabled = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="IsPinchEnabled"/>.</summary>
    public static readonly BindableProperty IsPinchEnabledProperty = BindableProperty.Create(nameof(IsPinchEnabled), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Applied(model => model.IsPinchEnabled = (bool)value));

    /// <summary>Whether a pinch zooms out (a display mode up) or in (default <c>true</c>).</summary>
    public bool IsPinchEnabled { get => (bool)GetValue(IsPinchEnabledProperty); set => SetValue(IsPinchEnabledProperty, value); }

    /// <summary>Sets <see cref="IsPinchEnabled"/>.</summary>
    public SkUiCalendar SetIsPinchEnabled(bool value)
    {
        IsPinchEnabled = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="IsHeaderVisible"/>.</summary>
    public static readonly BindableProperty IsHeaderVisibleProperty = BindableProperty.Create(nameof(IsHeaderVisible), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.IsHeaderVisible = (bool)value));

    /// <summary>Whether the header (title and previous / next buttons) shows (default <c>true</c>).</summary>
    public bool IsHeaderVisible { get => (bool)GetValue(IsHeaderVisibleProperty); set => SetValue(IsHeaderVisibleProperty, value); }

    /// <summary>Sets <see cref="IsHeaderVisible"/>.</summary>
    public SkUiCalendar SetIsHeaderVisible(bool value)
    {
        IsHeaderVisible = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderLayout"/>.</summary>
    public static readonly BindableProperty HeaderLayoutProperty = BindableProperty.Create(nameof(HeaderLayout), typeof(SkUiCalendarHeaderLayout), typeof(SkUiCalendar), SkUiCalendarHeaderLayout.TitleFirst,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarHeaderLayout)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderLayout = (SkUiCalendarHeaderLayout)value));

    /// <summary>Where the header puts its title and buttons (default: the title first, both buttons at the end).</summary>
    public SkUiCalendarHeaderLayout HeaderLayout { get => (SkUiCalendarHeaderLayout)GetValue(HeaderLayoutProperty); set => SetValue(HeaderLayoutProperty, value); }

    /// <summary>Sets <see cref="HeaderLayout"/>.</summary>
    public SkUiCalendar SetHeaderLayout(SkUiCalendarHeaderLayout value)
    {
        ThrowIfUndefined(value);
        HeaderLayout = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AreNavigationButtonsVisible"/>.</summary>
    public static readonly BindableProperty AreNavigationButtonsVisibleProperty = BindableProperty.Create(nameof(AreNavigationButtonsVisible), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.AreNavigationButtonsVisible = (bool)value));

    /// <summary>Whether the header's previous / next buttons show (default <c>true</c>).</summary>
    public bool AreNavigationButtonsVisible { get => (bool)GetValue(AreNavigationButtonsVisibleProperty); set => SetValue(AreNavigationButtonsVisibleProperty, value); }

    /// <summary>Sets <see cref="AreNavigationButtonsVisible"/>.</summary>
    public SkUiCalendar SetAreNavigationButtonsVisible(bool value)
    {
        AreNavigationButtonsVisible = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderTextColor"/>.</summary>
    public static readonly BindableProperty HeaderTextColorProperty = BindableProperty.Create(nameof(HeaderTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderTextColor = (Color?)value));

    /// <summary>The title's color; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? HeaderTextColor { get => (Color?)GetValue(HeaderTextColorProperty); set => SetValue(HeaderTextColorProperty, value); }

    /// <summary>Sets <see cref="HeaderTextColor"/>.</summary>
    public SkUiCalendar SetHeaderTextColor(Color? value)
    {
        HeaderTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderFontSize"/>.</summary>
    public static readonly BindableProperty HeaderFontSizeProperty = BindableProperty.Create(nameof(HeaderFontSize), typeof(double), typeof(SkUiCalendar), double.NaN,
        validateValue: FontSizeValue,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderFontSize = (double)value));

    /// <summary>The title's font size; NaN (default): the look's (<see cref="SkUiLook.CalendarHeaderFontSize"/>).</summary>
    public double HeaderFontSize { get => (double)GetValue(HeaderFontSizeProperty); set => SetValue(HeaderFontSizeProperty, value); }

    /// <summary>Sets <see cref="HeaderFontSize"/>.</summary>
    public SkUiCalendar SetHeaderFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        HeaderFontSize = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderFontAttributes"/>.</summary>
    public static readonly BindableProperty HeaderFontAttributesProperty = BindableProperty.Create(nameof(HeaderFontAttributes), typeof(FontAttributes), typeof(SkUiCalendar), FontAttributes.Bold,
        validateValue: SkUiValidate.Flags<FontAttributes>,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderFontAttributes = (FontAttributes)value));

    /// <summary>The title's font attributes (default bold).</summary>
    public FontAttributes HeaderFontAttributes { get => (FontAttributes)GetValue(HeaderFontAttributesProperty); set => SetValue(HeaderFontAttributesProperty, value); }

    /// <summary>Sets <see cref="HeaderFontAttributes"/>.</summary>
    public SkUiCalendar SetHeaderFontAttributes(FontAttributes value)
    {
        SkUiValidate.ThrowIfNotFlags(value, nameof(value));
        HeaderFontAttributes = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonColor"/>.</summary>
    public static readonly BindableProperty NavigationButtonColorProperty = BindableProperty.Create(nameof(NavigationButtonColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonColor = (Color?)value));

    /// <summary>The buttons' chevron color; <c>null</c>: the color scheme's accent.</summary>
    public Color? NavigationButtonColor { get => (Color?)GetValue(NavigationButtonColorProperty); set => SetValue(NavigationButtonColorProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonColor"/>.</summary>
    public SkUiCalendar SetNavigationButtonColor(Color? value)
    {
        NavigationButtonColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonBackgroundColor"/>.</summary>
    public static readonly BindableProperty NavigationButtonBackgroundColorProperty = BindableProperty.Create(nameof(NavigationButtonBackgroundColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonBackgroundColor = (Color?)value));

    /// <summary>A circle behind each button; <c>null</c> (default): none.</summary>
    public Color? NavigationButtonBackgroundColor { get => (Color?)GetValue(NavigationButtonBackgroundColorProperty); set => SetValue(NavigationButtonBackgroundColorProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonBackgroundColor"/>.</summary>
    public SkUiCalendar SetNavigationButtonBackgroundColor(Color? value)
    {
        NavigationButtonBackgroundColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonSize"/>.</summary>
    public static readonly BindableProperty NavigationButtonSizeProperty = BindableProperty.Create(nameof(NavigationButtonSize), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonSize = (double)value));

    /// <summary>The side of the header's previous / next buttons (their touch target); negative (default -1): the look's (<see cref="SkUiLook.CalendarNavigationButtonSize"/>, 36). Kept within the header's height.</summary>
    public double NavigationButtonSize { get => (double)GetValue(NavigationButtonSizeProperty); set => SetValue(NavigationButtonSizeProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonSize"/>.</summary>
    public SkUiCalendar SetNavigationButtonSize(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        NavigationButtonSize = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonSpacing"/>.</summary>
    public static readonly BindableProperty NavigationButtonSpacingProperty = BindableProperty.Create(nameof(NavigationButtonSpacing), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonSpacing = (double)value));

    /// <summary>The gap between the two buttons when they sit together at the end, and between a button and the centered title; negative (default -1): 4.</summary>
    public double NavigationButtonSpacing { get => (double)GetValue(NavigationButtonSpacingProperty); set => SetValue(NavigationButtonSpacingProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonSpacing"/>.</summary>
    public SkUiCalendar SetNavigationButtonSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        NavigationButtonSpacing = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonCornerRadius"/>.</summary>
    public static readonly BindableProperty NavigationButtonCornerRadiusProperty = BindableProperty.Create(nameof(NavigationButtonCornerRadius), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonCornerRadius = (double)value));

    /// <summary>The corner radius of the buttons' background and border; negative (default -1): a circle.</summary>
    public double NavigationButtonCornerRadius { get => (double)GetValue(NavigationButtonCornerRadiusProperty); set => SetValue(NavigationButtonCornerRadiusProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonCornerRadius"/>.</summary>
    public SkUiCalendar SetNavigationButtonCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        NavigationButtonCornerRadius = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonBorderColor"/>.</summary>
    public static readonly BindableProperty NavigationButtonBorderColorProperty = BindableProperty.Create(nameof(NavigationButtonBorderColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonBorderColor = (Color?)value));

    /// <summary>The buttons' border color; <c>null</c> (default): none.</summary>
    public Color? NavigationButtonBorderColor { get => (Color?)GetValue(NavigationButtonBorderColorProperty); set => SetValue(NavigationButtonBorderColorProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonBorderColor"/>.</summary>
    public SkUiCalendar SetNavigationButtonBorderColor(Color? value)
    {
        NavigationButtonBorderColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonBorderWidth"/>.</summary>
    public static readonly BindableProperty NavigationButtonBorderWidthProperty = BindableProperty.Create(nameof(NavigationButtonBorderWidth), typeof(double), typeof(SkUiCalendar), 0d,
        validateValue: NonNegativeNumber,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonBorderWidth = (double)value));

    /// <summary>The buttons' border width (default 0: none).</summary>
    public double NavigationButtonBorderWidth { get => (double)GetValue(NavigationButtonBorderWidthProperty); set => SetValue(NavigationButtonBorderWidthProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonBorderWidth"/>.</summary>
    public SkUiCalendar SetNavigationButtonBorderWidth(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        NavigationButtonBorderWidth = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="PreviousButtonGlyph"/>.</summary>
    public static readonly BindableProperty PreviousButtonGlyphProperty = BindableProperty.Create(nameof(PreviousButtonGlyph), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.PreviousButtonGlyph = (string?)value));

    /// <summary>An icon for the previous button instead of the look's chevron: a glyph of <see cref="NavigationButtonFontFamily"/> (an icon font), in <see cref="NavigationButtonColor"/>; mirrored right-to-left. <c>null</c> (default): the chevron.</summary>
    public string? PreviousButtonGlyph { get => (string?)GetValue(PreviousButtonGlyphProperty); set => SetValue(PreviousButtonGlyphProperty, value); }

    /// <summary>Sets <see cref="PreviousButtonGlyph"/>.</summary>
    public SkUiCalendar SetPreviousButtonGlyph(string? value)
    {
        PreviousButtonGlyph = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NextButtonGlyph"/>.</summary>
    public static readonly BindableProperty NextButtonGlyphProperty = BindableProperty.Create(nameof(NextButtonGlyph), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NextButtonGlyph = (string?)value));

    /// <summary>An icon for the next button instead of the look's chevron (see <see cref="PreviousButtonGlyph"/>).</summary>
    public string? NextButtonGlyph { get => (string?)GetValue(NextButtonGlyphProperty); set => SetValue(NextButtonGlyphProperty, value); }

    /// <summary>Sets <see cref="NextButtonGlyph"/>.</summary>
    public SkUiCalendar SetNextButtonGlyph(string? value)
    {
        NextButtonGlyph = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="PreviousButtonIcon"/>.</summary>
    public static readonly BindableProperty PreviousButtonIconProperty = BindableProperty.Create(nameof(PreviousButtonIcon), typeof(ImageSource), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.PreviousButtonIcon = SkUiImageSource.FromMaui((ImageSource?)value)));

    /// <summary>An image for the previous button instead of the glyph or the look's chevron (a PNG, an SVG, a URL: any source the image loader reads), drawn at <see cref="NavigationButtonGlyphSize"/> and mirrored right-to-left; tint an SVG with <c>SkUiImageSource.FromVector(source, color)</c>. <c>null</c> (default): none.</summary>
    public ImageSource? PreviousButtonIcon { get => (ImageSource?)GetValue(PreviousButtonIconProperty); set => SetValue(PreviousButtonIconProperty, value); }

    /// <summary>Sets <see cref="PreviousButtonIcon"/>.</summary>
    public SkUiCalendar SetPreviousButtonIcon(ImageSource? value) { PreviousButtonIcon = value; return this; }

    /// <summary>Bindable property for <see cref="NextButtonIcon"/>.</summary>
    public static readonly BindableProperty NextButtonIconProperty = BindableProperty.Create(nameof(NextButtonIcon), typeof(ImageSource), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NextButtonIcon = SkUiImageSource.FromMaui((ImageSource?)value)));

    /// <summary>An image for the next button (see <see cref="PreviousButtonIcon"/>).</summary>
    public ImageSource? NextButtonIcon { get => (ImageSource?)GetValue(NextButtonIconProperty); set => SetValue(NextButtonIconProperty, value); }

    /// <summary>Sets <see cref="NextButtonIcon"/>.</summary>
    public SkUiCalendar SetNextButtonIcon(ImageSource? value) { NextButtonIcon = value; return this; }

    /// <summary>Bindable property for <see cref="NavigationButtonFontFamily"/>.</summary>
    public static readonly BindableProperty NavigationButtonFontFamilyProperty = BindableProperty.Create(nameof(NavigationButtonFontFamily), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonFontFamily = (string?)value));

    /// <summary>The font of <see cref="PreviousButtonGlyph"/> and <see cref="NextButtonGlyph"/> (an icon font registered with the app); <c>null</c>: <see cref="FontFamily"/>.</summary>
    public string? NavigationButtonFontFamily { get => (string?)GetValue(NavigationButtonFontFamilyProperty); set => SetValue(NavigationButtonFontFamilyProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonFontFamily"/>.</summary>
    public SkUiCalendar SetNavigationButtonFontFamily(string? value)
    {
        NavigationButtonFontFamily = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="NavigationButtonGlyphSize"/>.</summary>
    public static readonly BindableProperty NavigationButtonGlyphSizeProperty = BindableProperty.Create(nameof(NavigationButtonGlyphSize), typeof(double), typeof(SkUiCalendar), double.NaN,
        validateValue: FontSizeValue,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.NavigationButtonGlyphSize = (double)value));

    /// <summary>The glyphs' font size; NaN (default): half the button's side.</summary>
    public double NavigationButtonGlyphSize { get => (double)GetValue(NavigationButtonGlyphSizeProperty); set => SetValue(NavigationButtonGlyphSizeProperty, value); }

    /// <summary>Sets <see cref="NavigationButtonGlyphSize"/>.</summary>
    public SkUiCalendar SetNavigationButtonGlyphSize(double value)
    {
        ThrowIfNotFontSize(value);
        NavigationButtonGlyphSize = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderFontFamily"/>.</summary>
    public static readonly BindableProperty HeaderFontFamilyProperty = BindableProperty.Create(nameof(HeaderFontFamily), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderFontFamily = (string?)value));

    /// <summary>The title's font family; <c>null</c>: <see cref="FontFamily"/>.</summary>
    public string? HeaderFontFamily { get => (string?)GetValue(HeaderFontFamilyProperty); set => SetValue(HeaderFontFamilyProperty, value); }

    /// <summary>Sets <see cref="HeaderFontFamily"/>.</summary>
    public SkUiCalendar SetHeaderFontFamily(string? value)
    {
        HeaderFontFamily = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderHeight"/>.</summary>
    public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create(nameof(HeaderHeight), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.HeaderHeight = (double)value));

    /// <summary>The header's height; negative (default -1): the look's (<see cref="SkUiLook.CalendarHeaderHeight"/>, 48).</summary>
    public double HeaderHeight { get => (double)GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }

    /// <summary>Sets <see cref="HeaderHeight"/>.</summary>
    public SkUiCalendar SetHeaderHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        HeaderHeight = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="HeaderPadding"/>.</summary>
    public static readonly BindableProperty HeaderPaddingProperty = BindableProperty.Create(nameof(HeaderPadding), typeof(Thickness), typeof(SkUiCalendar), new Thickness(8, 0),
        validateValue: SkUiValidate.ThicknessValue,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.HeaderPadding = (Thickness)value));

    /// <summary>The space inside the header around the title and the buttons (default 8 at the sides): line the title up with the days, or inset the buttons.</summary>
    public Thickness HeaderPadding { get => (Thickness)GetValue(HeaderPaddingProperty); set => SetValue(HeaderPaddingProperty, value); }

    /// <summary>Sets <see cref="HeaderPadding"/>.</summary>
    public SkUiCalendar SetHeaderPadding(Thickness value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        HeaderPadding = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AreDayNamesVisible"/>.</summary>
    public static readonly BindableProperty AreDayNamesVisibleProperty = BindableProperty.Create(nameof(AreDayNamesVisible), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.AreDayNamesVisible = (bool)value));

    /// <summary>Whether the row of day names shows above the days (default <c>true</c>).</summary>
    public bool AreDayNamesVisible { get => (bool)GetValue(AreDayNamesVisibleProperty); set => SetValue(AreDayNamesVisibleProperty, value); }

    /// <summary>Sets <see cref="AreDayNamesVisible"/>.</summary>
    public SkUiCalendar SetAreDayNamesVisible(bool value)
    {
        AreDayNamesVisible = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayNameFormat"/>.</summary>
    public static readonly BindableProperty DayNameFormatProperty = BindableProperty.Create(nameof(DayNameFormat), typeof(SkUiCalendarDayNameFormat), typeof(SkUiCalendar), SkUiCalendarDayNameFormat.Shortest,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarDayNameFormat)value),
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.DayNameFormat = (SkUiCalendarDayNameFormat)value));

    /// <summary>How the day names are written (default the culture's shortest names).</summary>
    public SkUiCalendarDayNameFormat DayNameFormat { get => (SkUiCalendarDayNameFormat)GetValue(DayNameFormatProperty); set => SetValue(DayNameFormatProperty, value); }

    /// <summary>Sets <see cref="DayNameFormat"/>.</summary>
    public SkUiCalendar SetDayNameFormat(SkUiCalendarDayNameFormat value)
    {
        ThrowIfUndefined(value);
        DayNameFormat = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayNameTextColor"/>.</summary>
    public static readonly BindableProperty DayNameTextColorProperty = BindableProperty.Create(nameof(DayNameTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.DayNameTextColor = (Color?)value));

    /// <summary>The day names' and week numbers' color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? DayNameTextColor { get => (Color?)GetValue(DayNameTextColorProperty); set => SetValue(DayNameTextColorProperty, value); }

    /// <summary>Sets <see cref="DayNameTextColor"/>.</summary>
    public SkUiCalendar SetDayNameTextColor(Color? value)
    {
        DayNameTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayNameFontSize"/>.</summary>
    public static readonly BindableProperty DayNameFontSizeProperty = BindableProperty.Create(nameof(DayNameFontSize), typeof(double), typeof(SkUiCalendar), double.NaN,
        validateValue: FontSizeValue,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.DayNameFontSize = (double)value));

    /// <summary>The day names' and week numbers' font size; NaN (default): the look's (<see cref="SkUiLook.CalendarDayNameFontSize"/>).</summary>
    public double DayNameFontSize { get => (double)GetValue(DayNameFontSizeProperty); set => SetValue(DayNameFontSizeProperty, value); }

    /// <summary>Sets <see cref="DayNameFontSize"/>.</summary>
    public SkUiCalendar SetDayNameFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        DayNameFontSize = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayNameFontAttributes"/>.</summary>
    public static readonly BindableProperty DayNameFontAttributesProperty = BindableProperty.Create(nameof(DayNameFontAttributes), typeof(FontAttributes), typeof(SkUiCalendar), FontAttributes.None,
        validateValue: SkUiValidate.Flags<FontAttributes>,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.DayNameFontAttributes = (FontAttributes)value));

    /// <summary>The day names' font attributes (default none).</summary>
    public FontAttributes DayNameFontAttributes { get => (FontAttributes)GetValue(DayNameFontAttributesProperty); set => SetValue(DayNameFontAttributesProperty, value); }

    /// <summary>Sets <see cref="DayNameFontAttributes"/>.</summary>
    public SkUiCalendar SetDayNameFontAttributes(FontAttributes value)
    {
        SkUiValidate.ThrowIfNotFlags(value, nameof(value));
        DayNameFontAttributes = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="IsTodayDayNameHighlighted"/>.</summary>
    public static readonly BindableProperty IsTodayDayNameHighlightedProperty = BindableProperty.Create(nameof(IsTodayDayNameHighlighted), typeof(bool),
        typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.IsTodayDayNameHighlighted = (bool)value));

    /// <summary>Whether today's day name is drawn in <see cref="TodayDayNameTextColor"/> while the page shows today (default <c>true</c>).</summary>
    public bool IsTodayDayNameHighlighted { get => (bool)GetValue(IsTodayDayNameHighlightedProperty); set => SetValue(IsTodayDayNameHighlightedProperty, value); }

    /// <summary>Sets <see cref="IsTodayDayNameHighlighted"/>.</summary>
    public SkUiCalendar SetIsTodayDayNameHighlighted(bool value) { IsTodayDayNameHighlighted = value; return this; }

    /// <summary>Bindable property for <see cref="TodayDayNameTextColor"/>.</summary>
    public static readonly BindableProperty TodayDayNameTextColorProperty = BindableProperty.Create(nameof(TodayDayNameTextColor), typeof(Color),
        typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.TodayDayNameTextColor = (Color?)value));

    /// <summary>Today's day name color; <c>null</c>: <see cref="TodayColor"/> (the color scheme's accent).</summary>
    public Color? TodayDayNameTextColor { get => (Color?)GetValue(TodayDayNameTextColorProperty); set => SetValue(TodayDayNameTextColorProperty, value); }

    /// <summary>Sets <see cref="TodayDayNameTextColor"/>.</summary>
    public SkUiCalendar SetTodayDayNameTextColor(Color? value) { TodayDayNameTextColor = value; return this; }

    /// <summary>Bindable property for <see cref="AreAdjacentDaysVisible"/>.</summary>
    public static readonly BindableProperty AreAdjacentDaysVisibleProperty = BindableProperty.Create(nameof(AreAdjacentDaysVisible), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.AreAdjacentDaysVisible = (bool)value));

    /// <summary>Whether the neighbor months' days fill the first and last weeks (default <c>true</c>; a tap on one shows its month). Also the first and last cells of decade and century pages.</summary>
    public bool AreAdjacentDaysVisible { get => (bool)GetValue(AreAdjacentDaysVisibleProperty); set => SetValue(AreAdjacentDaysVisibleProperty, value); }

    /// <summary>Sets <see cref="AreAdjacentDaysVisible"/>.</summary>
    public SkUiCalendar SetAreAdjacentDaysVisible(bool value)
    {
        AreAdjacentDaysVisible = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AreWeekNumbersVisible"/>.</summary>
    public static readonly BindableProperty AreWeekNumbersVisibleProperty = BindableProperty.Create(nameof(AreWeekNumbersVisible), typeof(bool), typeof(SkUiCalendar), false,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.AreWeekNumbersVisible = (bool)value));

    /// <summary>Whether a column of week numbers shows at the start (default <c>false</c>), by the culture's week rule (ISO 8601 for Monday weeks).</summary>
    public bool AreWeekNumbersVisible { get => (bool)GetValue(AreWeekNumbersVisibleProperty); set => SetValue(AreWeekNumbersVisibleProperty, value); }

    /// <summary>Sets <see cref="AreWeekNumbersVisible"/>.</summary>
    public SkUiCalendar SetAreWeekNumbersVisible(bool value)
    {
        AreWeekNumbersVisible = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TextColor"/>.</summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.TextColor = (Color?)value));

    /// <summary>The days', months' and years' color; <c>null</c>: the color scheme's foreground.</summary>
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCalendar SetTextColor(Color? value)
    {
        TextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="FontFamily"/>.</summary>
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.FontFamily = (string?)value));

    /// <summary>The font family of every text of the calendar; <c>null</c>: the look's.</summary>
    public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCalendar SetFontFamily(string? value)
    {
        FontFamily = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="FontSize"/>.</summary>
    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(nameof(FontSize), typeof(double), typeof(SkUiCalendar), double.NaN,
        validateValue: FontSizeValue,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.FontSize = (double)value));

    /// <summary>The days', months' and years' font size; NaN (default): the look's (<see cref="SkUiLook.CalendarFontSize"/>).</summary>
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCalendar SetFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        FontSize = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="FontAttributes"/>.</summary>
    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(nameof(FontAttributes), typeof(FontAttributes), typeof(SkUiCalendar), FontAttributes.None,
        validateValue: SkUiValidate.Flags<FontAttributes>,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.FontAttributes = (FontAttributes)value));

    /// <summary>The days', months' and years' font attributes (default none; today has <see cref="TodayFontAttributes"/>).</summary>
    public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty); set => SetValue(FontAttributesProperty, value); }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCalendar SetFontAttributes(FontAttributes value)
    {
        SkUiValidate.ThrowIfNotFlags(value, nameof(value));
        FontAttributes = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="FontAutoScalingEnabled"/>.</summary>
    public static readonly BindableProperty FontAutoScalingEnabledProperty = BindableProperty.Create(nameof(FontAutoScalingEnabled), typeof(bool), typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.FontAutoScalingEnabled = (bool)value));

    /// <summary>Whether the texts follow the system's text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty); set => SetValue(FontAutoScalingEnabledProperty, value); }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCalendar SetFontAutoScalingEnabled(bool value)
    {
        FontAutoScalingEnabled = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DayRowHeight"/>.</summary>
    public static readonly BindableProperty DayRowHeightProperty = BindableProperty.Create(nameof(DayRowHeight), typeof(double), typeof(SkUiCalendar), -1d,
        validateValue: SkUiValidate.Finite,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Measured(model => model.DayRowHeight = (double)value));

    /// <summary>A week row's height when the calendar sizes itself; negative (default -1): the look's (<see cref="SkUiLook.CalendarRowHeight"/>). Rows stretch to a taller calendar.</summary>
    public double DayRowHeight { get => (double)GetValue(DayRowHeightProperty); set => SetValue(DayRowHeightProperty, value); }

    /// <summary>Sets <see cref="DayRowHeight"/>.</summary>
    public SkUiCalendar SetDayRowHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        DayRowHeight = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TodayStyle"/>.</summary>
    public static readonly BindableProperty TodayStyleProperty = BindableProperty.Create(nameof(TodayStyle), typeof(SkUiCalendarTodayStyle), typeof(SkUiCalendar), SkUiCalendarTodayStyle.Ring,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarTodayStyle)value),
        propertyChanged: (view, _, _) => ((SkUiCalendar)view).Painted(static _ => { }));

    /// <summary>
    /// How today is marked. Not set: the look's (<see cref="SkUiLook.CalendarTodayStyle"/>, 1.15): a ring on the default look, the
    /// number in the accent on iOS and Mac Catalyst, a filled circle on Windows. The getter reads <see cref="SkUiCalendarTodayStyle.Ring"/>
    /// while it is not set.
    /// </summary>
    public SkUiCalendarTodayStyle TodayStyle { get => (SkUiCalendarTodayStyle)GetValue(TodayStyleProperty); set => SetValue(TodayStyleProperty, value); }

    /// <summary>Sets <see cref="TodayStyle"/>.</summary>
    public SkUiCalendar SetTodayStyle(SkUiCalendarTodayStyle value)
    {
        ThrowIfUndefined(value);
        TodayStyle = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TodayColor"/>.</summary>
    public static readonly BindableProperty TodayColorProperty = BindableProperty.Create(nameof(TodayColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.TodayColor = (Color?)value));

    /// <summary>Today's ring, fill or text color; <c>null</c>: the color scheme's accent.</summary>
    public Color? TodayColor { get => (Color?)GetValue(TodayColorProperty); set => SetValue(TodayColorProperty, value); }

    /// <summary>Sets <see cref="TodayColor"/>.</summary>
    public SkUiCalendar SetTodayColor(Color? value)
    {
        TodayColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TodayTextColor"/>.</summary>
    public static readonly BindableProperty TodayTextColorProperty = BindableProperty.Create(nameof(TodayTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.TodayTextColor = (Color?)value));

    /// <summary>Today's number color; <c>null</c>: <see cref="TextColor"/> (a contrasting color when filled, <see cref="TodayColor"/> for the text style).</summary>
    public Color? TodayTextColor { get => (Color?)GetValue(TodayTextColorProperty); set => SetValue(TodayTextColorProperty, value); }

    /// <summary>Sets <see cref="TodayTextColor"/>.</summary>
    public SkUiCalendar SetTodayTextColor(Color? value)
    {
        TodayTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="TodayFontAttributes"/>.</summary>
    public static readonly BindableProperty TodayFontAttributesProperty = BindableProperty.Create(nameof(TodayFontAttributes), typeof(FontAttributes), typeof(SkUiCalendar), FontAttributes.Bold,
        validateValue: SkUiValidate.Flags<FontAttributes>,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.TodayFontAttributes = (FontAttributes)value));

    /// <summary>Today's font attributes (default bold).</summary>
    public FontAttributes TodayFontAttributes { get => (FontAttributes)GetValue(TodayFontAttributesProperty); set => SetValue(TodayFontAttributesProperty, value); }

    /// <summary>Sets <see cref="TodayFontAttributes"/>.</summary>
    public SkUiCalendar SetTodayFontAttributes(FontAttributes value)
    {
        SkUiValidate.ThrowIfNotFlags(value, nameof(value));
        TodayFontAttributes = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="SelectionShape"/>.</summary>
    public static readonly BindableProperty SelectionShapeProperty = BindableProperty.Create(nameof(SelectionShape), typeof(SkUiCalendarSelectionShape), typeof(SkUiCalendar), SkUiCalendarSelectionShape.Circle,
        validateValue: (_, value) => Enum.IsDefined((SkUiCalendarSelectionShape)value),
        propertyChanged: (view, _, _) => ((SkUiCalendar)view).Painted(static _ => { }));

    /// <summary>
    /// The shape behind a selected day. Not set: the look's (<see cref="SkUiLook.CalendarSelectionShape"/>, 1.15): a circle, a
    /// rounded square on Mac Catalyst. The getter reads <see cref="SkUiCalendarSelectionShape.Circle"/> while it is not set.
    /// </summary>
    public SkUiCalendarSelectionShape SelectionShape { get => (SkUiCalendarSelectionShape)GetValue(SelectionShapeProperty); set => SetValue(SelectionShapeProperty, value); }

    /// <summary>Sets <see cref="SelectionShape"/>.</summary>
    public SkUiCalendar SetSelectionShape(SkUiCalendarSelectionShape value)
    {
        ThrowIfUndefined(value);
        SelectionShape = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="SelectionColor"/>.</summary>
    public static readonly BindableProperty SelectionColorProperty = BindableProperty.Create(nameof(SelectionColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.SelectionColor = (Color?)value));

    /// <summary>The selected days' fill (and a range's ends); <c>null</c>: the look's from the color scheme's accent (<see cref="SkUiLook.CalendarSelectionColor"/>: the accent itself, a light tint of it on iOS).</summary>
    public Color? SelectionColor { get => (Color?)GetValue(SelectionColorProperty); set => SetValue(SelectionColorProperty, value); }

    /// <summary>Sets <see cref="SelectionColor"/>.</summary>
    public SkUiCalendar SetSelectionColor(Color? value)
    {
        SelectionColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="SelectedTextColor"/>.</summary>
    public static readonly BindableProperty SelectedTextColorProperty = BindableProperty.Create(nameof(SelectedTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.SelectedTextColor = (Color?)value));

    /// <summary>The selected days' number color; <c>null</c>: the look's (<see cref="SkUiLook.CalendarSelectedTextColor"/>: white or near black, whichever reads on the selection; the accent on iOS's light tint).</summary>
    public Color? SelectedTextColor { get => (Color?)GetValue(SelectedTextColorProperty); set => SetValue(SelectedTextColorProperty, value); }

    /// <summary>Sets <see cref="SelectedTextColor"/>.</summary>
    public SkUiCalendar SetSelectedTextColor(Color? value)
    {
        SelectedTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="RangeColor"/>.</summary>
    public static readonly BindableProperty RangeColorProperty = BindableProperty.Create(nameof(RangeColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.RangeColor = (Color?)value));

    /// <summary>The band between a range's first and last days; <c>null</c>: <see cref="SelectionColor"/>, light.</summary>
    public Color? RangeColor { get => (Color?)GetValue(RangeColorProperty); set => SetValue(RangeColorProperty, value); }

    /// <summary>Sets <see cref="RangeColor"/>.</summary>
    public SkUiCalendar SetRangeColor(Color? value)
    {
        RangeColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="RangeTextColor"/>.</summary>
    public static readonly BindableProperty RangeTextColorProperty = BindableProperty.Create(nameof(RangeTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.RangeTextColor = (Color?)value));

    /// <summary>The number color of the days between a range's ends; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? RangeTextColor { get => (Color?)GetValue(RangeTextColorProperty); set => SetValue(RangeTextColorProperty, value); }

    /// <summary>Sets <see cref="RangeTextColor"/>.</summary>
    public SkUiCalendar SetRangeTextColor(Color? value)
    {
        RangeTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AdjacentDayTextColor"/>.</summary>
    public static readonly BindableProperty AdjacentDayTextColorProperty = BindableProperty.Create(nameof(AdjacentDayTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.AdjacentDayTextColor = (Color?)value));

    /// <summary>The neighbor months' days' color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? AdjacentDayTextColor { get => (Color?)GetValue(AdjacentDayTextColorProperty); set => SetValue(AdjacentDayTextColorProperty, value); }

    /// <summary>Sets <see cref="AdjacentDayTextColor"/>.</summary>
    public SkUiCalendar SetAdjacentDayTextColor(Color? value)
    {
        AdjacentDayTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="DisabledDayTextColor"/>.</summary>
    public static readonly BindableProperty DisabledDayTextColorProperty = BindableProperty.Create(nameof(DisabledDayTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.DisabledDayTextColor = (Color?)value));

    /// <summary>The color of days that cannot be chosen; <c>null</c>: the color scheme's disabled color.</summary>
    public Color? DisabledDayTextColor { get => (Color?)GetValue(DisabledDayTextColorProperty); set => SetValue(DisabledDayTextColorProperty, value); }

    /// <summary>Sets <see cref="DisabledDayTextColor"/>.</summary>
    public SkUiCalendar SetDisabledDayTextColor(Color? value)
    {
        DisabledDayTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="WeekendTextColor"/>.</summary>
    public static readonly BindableProperty WeekendTextColorProperty = BindableProperty.Create(nameof(WeekendTextColor), typeof(Color), typeof(SkUiCalendar), null,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.WeekendTextColor = (Color?)value));

    /// <summary>Saturdays' and Sundays' color; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? WeekendTextColor { get => (Color?)GetValue(WeekendTextColorProperty); set => SetValue(WeekendTextColorProperty, value); }

    /// <summary>Sets <see cref="WeekendTextColor"/>.</summary>
    public SkUiCalendar SetWeekendTextColor(Color? value)
    {
        WeekendTextColor = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AllowsDeselection"/>.</summary>
    public static readonly BindableProperty AllowsDeselectionProperty = BindableProperty.Create(nameof(AllowsDeselection), typeof(bool), typeof(SkUiCalendar), false,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Applied(model => model.AllowsDeselection = (bool)value));

    /// <summary>Whether a tap on the selected day clears the single selection (default <c>false</c>). Multiple selection always toggles.</summary>
    public bool AllowsDeselection { get => (bool)GetValue(AllowsDeselectionProperty); set => SetValue(AllowsDeselectionProperty, value); }

    /// <summary>Sets <see cref="AllowsDeselection"/>.</summary>
    public SkUiCalendar SetAllowsDeselection(bool value)
    {
        AllowsDeselection = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="MinimumRangeDays"/>.</summary>
    public static readonly BindableProperty MinimumRangeDaysProperty = BindableProperty.Create(nameof(MinimumRangeDays), typeof(int), typeof(SkUiCalendar), 0,
        validateValue: (_, value) => (int)value >= 0,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.MinimumRangeDays = (int)value));

    /// <summary>
    /// The fewest days a range the user chooses may have, counting both ends (a stay of at least two nights: 3); 0 (default) or 1:
    /// any. While a range waits for its end, days that would end it too short are dimmed and cannot be tapped. The app's own ranges
    /// are not checked.
    /// </summary>
    public int MinimumRangeDays { get => (int)GetValue(MinimumRangeDaysProperty); set => SetValue(MinimumRangeDaysProperty, value); }

    /// <summary>Sets <see cref="MinimumRangeDays"/>.</summary>
    public SkUiCalendar SetMinimumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        MinimumRangeDays = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="MaximumRangeDays"/>.</summary>
    public static readonly BindableProperty MaximumRangeDaysProperty = BindableProperty.Create(nameof(MaximumRangeDays), typeof(int), typeof(SkUiCalendar), 0,
        validateValue: (_, value) => (int)value >= 0,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.MaximumRangeDays = (int)value));

    /// <summary>
    /// The most days a range the user chooses may have, counting both ends (a fortnight: 14); 0 (default): any. While a range waits
    /// for its end, days that would end it too long are dimmed and cannot be tapped. The app's own ranges are not checked.
    /// </summary>
    public int MaximumRangeDays { get => (int)GetValue(MaximumRangeDaysProperty); set => SetValue(MaximumRangeDaysProperty, value); }

    /// <summary>Sets <see cref="MaximumRangeDays"/>.</summary>
    public SkUiCalendar SetMaximumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        MaximumRangeDays = value;
        return this;
    }

    /// <summary>Bindable property for <see cref="AllowsDisabledDaysInRange"/>.</summary>
    public static readonly BindableProperty AllowsDisabledDaysInRangeProperty = BindableProperty.Create(nameof(AllowsDisabledDaysInRange), typeof(bool),
        typeof(SkUiCalendar), true,
        propertyChanged: (view, _, value) => ((SkUiCalendar)view).Painted(model => model.AllowsDisabledDaysInRange = (bool)value));

    /// <summary>
    /// Whether a range the user chooses may run over days that cannot be chosen (default <c>true</c>: a week with a blackout day in
    /// it). <c>false</c> (a booking that cannot span a booked night): while a range waits for its end, the days past the nearest day
    /// that cannot be chosen are dimmed and cannot be tapped.
    /// </summary>
    public bool AllowsDisabledDaysInRange { get => (bool)GetValue(AllowsDisabledDaysInRangeProperty); set => SetValue(AllowsDisabledDaysInRangeProperty, value); }

    /// <summary>Sets <see cref="AllowsDisabledDaysInRange"/>.</summary>
    public SkUiCalendar SetAllowsDisabledDaysInRange(bool value)
    {
        AllowsDisabledDaysInRange = value;
        return this;
    }
    #endregion

    #region Property plumbing

    private static bool NonNegativeNumber(BindableObject bindable, object? value) => value is double number && double.IsFinite(number) && number >= 0;

    private static bool FontSizeValue(BindableObject bindable, object? value) => value is double size && (double.IsNaN(size) || double.IsFinite(size) && size >= 0);

    private static void ThrowIfNotFontSize(double value)
    {
        if (!double.IsNaN(value))
            SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
    }

    private static void ThrowIfUndefined<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private void Applied(Action<SkUiCalendarModel> apply) => apply(Model);

    private void Painted(Action<SkUiCalendarModel> apply)
    {
        apply(Model);
        InvalidatePaint();
        InvalidateSemantics();
    }

    private void Measured(Action<SkUiCalendarModel> apply)
    {
        apply(Model);
        InvalidateMeasureOverride();
        InvalidatePaint();
    }

    /// <summary>A setting that shapes the page changed: the mode and date are kept within the limits, and written back if they moved.</summary>
    private void Revalidated(Action<SkUiCalendarModel> apply, bool measure)
    {
        apply(Model);
        Model.Revalidate();
        SyncDisplay();
        if (measure)
            InvalidateMeasureOverride();
        InvalidatePaint();
    }

    private void OnRangeChanged(Action<SkUiCalendarModel> apply)
    {
        apply(Model);
        Model.SetDisplayDate(Model.DisplayDate);
        SyncDisplay();
        InvalidatePaint();
    }

    private void OnDisplayDateChanged(DateTime value)
    {
        if (_syncing)
            return;
        Model.SetDisplayDate(value);
        DisplayStateChanged();
    }

    private void OnDisplayModeChanged(SkUiCalendarDisplayMode value)
    {
        if (_syncing)
            return;
        Model.SetDisplayMode(value);
        DisplayStateChanged();
    }

    private void OnSelectedDateChanged(DateTime? value)
    {
        if (_syncing)
            return;
        if (Model.SetSelectedDate(value) is { } args)
            SelectionDone(args, writeBack: false);
    }

    private void OnSelectedDatesChanged(IReadOnlyList<DateTime>? value)
    {
        if (_syncing)
            return;
        if (Model.SetSelectedDates(value) is { } args)
        {
            // The property holds the engine's sorted list, not the app's (a new list, so this runs again with nothing to do).
            Sync(() => WriteBack(SelectedDatesProperty, Model.SelectedDates));
            SelectionDone(args, writeBack: false);
        }
    }

    private void OnSelectedRangeChanged(SkUiDateRange? value)
    {
        if (_syncing)
            return;
        if (Model.SetSelectedRange(value) is { } args)
            SelectionDone(args, writeBack: false);
    }

    private void OnSelectedRangesChanged(IReadOnlyList<SkUiDateRange>? value)
    {
        if (_syncing)
            return;
        if (Model.SetSelectedRanges(value) is { } args)
        {
            // The property holds the engine's ordered, joined list (as SelectedDates).
            Sync(() => WriteBack(SelectedRangesProperty, Model.SelectedRanges));
            SelectionDone(args);
        }
    }

    private IList<DateTime> CreateBlackoutDates()
    {
        var dates = new ObservableCollection<DateTime>();
        Model.BlackoutDates = dates;
        BlackoutListener.Listen(dates);
        return dates;
    }

    private void OnBlackoutDatesSet(IList<DateTime>? value)
    {
        Model.BlackoutDates = value;
        BlackoutListener.Listen(value);
        InvalidatePaint();
    }

    private void OnBlackoutDatesChanged()
    {
        Model.BlackoutDatesChanged();
        InvalidatePaint();
    }

    private void Sync(Action write)
    {
        var syncing = _syncing;
        _syncing = true;
        try
        {
            write();
        }
        finally
        {
            _syncing = syncing;
        }
    }

    /// <summary>Writes the engine's page and mode back to <see cref="DisplayDate"/> and <see cref="DisplayMode"/> when they differ.</summary>
    private void SyncDisplay()
    {
        Sync(() =>
        {
            if (DisplayDate != Model.DisplayDate)
                WriteBack(DisplayDateProperty, Model.DisplayDate);
            if (DisplayMode != Model.DisplayMode)
                WriteBack(DisplayModeProperty, Model.DisplayMode);
        });
        DisplayStateChanged();
    }

    /// <summary>Raises <see cref="Title"/>, <see cref="CanGoToPrevious"/> and <see cref="CanGoToNext"/> when the title changed; redraws.</summary>
    private void DisplayStateChanged()
    {
        var title = Model.Title;
        if (!ReferenceEquals(_reportedTitle, title))
        {
            _reportedTitle = title;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanGoToPrevious));
            OnPropertyChanged(nameof(CanGoToNext));
        }
        InvalidatePaint();
        InvalidateSemantics();
    }

    private void SelectionDone(SkUiCalendarSelectionChangedEventArgs args, bool writeBack = true)
    {
        if (writeBack)
            Sync(() =>
            {
                if (SelectedDate != Model.SelectedDate)
                    WriteBack(SelectedDateProperty, Model.SelectedDate);
                if (!ReferenceEquals(SelectedDates, Model.SelectedDates))
                    WriteBack(SelectedDatesProperty, Model.SelectedDates);
                if (!Nullable.Equals(SelectedRange, Model.SelectedRange))
                    WriteBack(SelectedRangeProperty, Model.SelectedRange);
                if (!ReferenceEquals(SelectedRanges, Model.SelectedRanges))
                    WriteBack(SelectedRangesProperty, Model.SelectedRanges);
                if (RangeSelectionTarget != Model.RangeSelectionTarget)
                    WriteBack(RangeSelectionTargetProperty, Model.RangeSelectionTarget);
            });
        InvalidatePaint();
        InvalidateSemantics();
        SelectionChanged?.Invoke(this, args);
    }

    #endregion

    #region Engine host, drawing and input

    bool ISkUiCalendarHost.CalendarRightToLeft => IsRightToLeft;

    // Read as drawn: not set (also after ClearValue, which raises no change when the value stays the same), the look's.
    SkUiCalendarTodayStyle? ISkUiCalendarHost.CalendarTodayStyle => IsSet(TodayStyleProperty) ? TodayStyle : null;

    SkUiCalendarSelectionShape? ISkUiCalendarHost.CalendarSelectionShape => IsSet(SelectionShapeProperty) ? SelectionShape : null;

    bool ISkUiCalendarHost.CalendarEnabled => IsEnabled;

    Size ISkUiCalendarHost.CalendarSize => new(Width, Height);

    void ISkUiCalendarHost.CalendarInvalidate(bool measure)
    {
        if (measure)
            InvalidateMeasureOverride();
        InvalidatePaint();
    }

    void ISkUiCalendarHost.CalendarDisplayChanged() => SyncDisplay();

    void ISkUiCalendarHost.CalendarSelectionChanged(SkUiCalendarSelectionChangedEventArgs args) => SelectionDone(args);

    void ISkUiCalendarHost.CalendarRangeTargetChanged() => Sync(() =>
    {
        if (RangeSelectionTarget != Model.RangeSelectionTarget)
            WriteBack(RangeSelectionTargetProperty, Model.RangeSelectionTarget);
    });

    void ISkUiCalendarHost.CalendarDateTapped(SkUiCalendarDateTappedEventArgs args) => DateTapped?.Invoke(this, args);

    void ISkUiCalendarHost.CalendarWeekNumberTapped(SkUiCalendarWeekNumberTappedEventArgs args) => WeekNumberTapped?.Invoke(this, args);

    /// <summary>
    /// Draws a header button the subclass's way: return <c>true</c> when it drew it, and the look's background, border,
    /// chevron and the icon are skipped. <paramref name="button"/> holds its bounds, direction, state and resolved colors;
    /// <see cref="DrawCalendarText"/> draws text through the calendar. The default draws nothing and returns <c>false</c>.
    /// </summary>
    protected virtual bool OnDrawNavigationButton(SKCanvas canvas, SkUiCalendarNavigationButtonPaint button, bool isNext) => false;

    /// <summary>
    /// Draws the header's title the subclass's way (a logo, a two-line title, a chevron that hints at the year view): return
    /// <c>true</c> when it drew it. A tap in <paramref name="bounds"/> still steps up a display mode. The default returns <c>false</c>.
    /// </summary>
    protected virtual bool OnDrawTitle(SKCanvas canvas, SKRect bounds, string title, SKColor color, bool isPressed) => false;

    /// <summary>
    /// Draws <paramref name="text"/> through the calendar's text engine (shaped, laid out once and kept), on one line in
    /// <paramref name="bounds"/>: for the drawing hooks. <paramref name="fontSize"/> NaN: the days' size; <paramref name="fontFamily"/>
    /// <c>null</c>: the calendar's.
    /// </summary>
    protected void DrawCalendarText(SKCanvas canvas, string text, SKRect bounds, SKColor color, double fontSize = double.NaN,
        FontAttributes attributes = FontAttributes.None, string? fontFamily = null, TextAlignment alignment = TextAlignment.Center) =>
        Model.DrawCustomText(canvas, text, bounds, color, fontSize, attributes, fontFamily, alignment);

    bool ISkUiCalendarHost.CalendarDrawNavigationButton(SKCanvas canvas, in SkUiCalendarNavigationButtonPaint button, bool isNext) =>
        OnDrawNavigationButton(canvas, button, isNext);

    bool ISkUiCalendarHost.CalendarDrawTitle(SKCanvas canvas, SKRect bounds, string title, SKColor color, bool isPressed) =>
        OnDrawTitle(canvas, bounds, title, color, isPressed);


    /// <summary>The engine (tests).</summary>
    internal SkUiCalendarModel Engine => Model;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => Model.Draw(canvas, (float)Width, (float)Height);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        recognizers.Add(_gesture ??= new SkUiCalendarGestureRecognizer(Model, () => new Size(Width, Height), () => _pinch?.HasTwoPointers == true));
        recognizers.Add(_pinch ??= new SkUiPinchGestureRecognizer
        {
            Handler = args =>
            {
                if (args.Status == GestureStatus.Completed)
                    Model.Pinch(args.TotalScale, args.Origin, (float)Width, (float)Height);
            }
        });
    }

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => Model.OnKey(key);

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        Model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => Model.OnSemanticsAction(action) || base.OnSemanticsAction(action);

    /// <inheritdoc />
    internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) => Model.OnSemanticsPartAction(part, action);

    /// <inheritdoc />
    internal override void OnEffectiveFlowDirectionChanged()
    {
        base.OnEffectiveFlowDirectionChanged();
        InvalidatePaint();
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        Model.Release();
    }

    #endregion
}
