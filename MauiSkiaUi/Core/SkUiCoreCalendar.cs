using System.Collections.ObjectModel;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// A drawn calendar (Core analogue of <see cref="SkUiCalendar"/>, on the same engine): a month of days, or the months of a
/// year, the years of a decade, the decades of a century, with a header, day names, today, a selection (one day, several,
/// or a range) and the date picker's day rules. One node, no view per day: the look draws the cells
/// (<see cref="SkUiLook.DrawCalendarCell"/>), the calendar their texts. Swipe or use the header's buttons to move between
/// pages; tap the title to step up a display mode, a month or year to step down, or pinch.
/// </summary>
/// <remarks>Every color is <c>null</c> by default: the color scheme's color when drawn, so the calendar follows a dark-mode switch.</remarks>
public class SkUiCoreCalendar : SkUiCoreNode, ISkUiCalendarHost, ISkUiKeyHandler, ISkUiFontElement
{
    private readonly SkUiCalendarModel _model;
    private readonly SkUiWeakListener<SkUiCoreCalendar> _blackoutListener;
    private SkUiCalendarGestureRecognizer? _gesture;
    private SkUiPinchGestureRecognizer? _pinch;
    private DateTime _reportedDisplay;
    private SkUiCalendarDisplayMode _reportedMode;
    private string? _reportedTitle;
    private SkUiCalendarRangeTarget _reportedTarget;

    /// <summary>Creates a calendar showing this month, with single selection.</summary>
    public SkUiCoreCalendar()
    {
        _model = new SkUiCalendarModel(this);
        _blackoutListener = new SkUiWeakListener<SkUiCoreCalendar>(this, static (calendar, _) => calendar.OnBlackoutDatesChanged());
        _reportedDisplay = _model.DisplayDate;
        var blackout = new ObservableCollection<DateTime>();
        _model.BlackoutDates = blackout;
        _blackoutListener.Listen(blackout);
        SkUiStrings.CurrentChanged += OnStringsChanged;
    }

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
    public bool CanGoToPrevious => _model.CanStep(-1);

    /// <summary>Whether the page can move on (not past <see cref="MaximumDate"/>'s page).</summary>
    public bool CanGoToNext => _model.CanStep(1);

    /// <summary>The header's text for the page shown ("November 2026", "2026", "2020 – 2029"), also when the header is hidden.</summary>
    public string Title => _model.Title;

    /// <summary>Shows the previous month (or year, decade, century), sliding when <paramref name="animated"/>; returns whether it moved.</summary>
    public bool GoToPrevious(bool animated = true) => _model.Step(-1, animated);

    /// <summary>Shows the next month (or year, decade, century), sliding when <paramref name="animated"/>; returns whether it moved.</summary>
    public bool GoToNext(bool animated = true) => _model.Step(1, animated);

    /// <summary>Shows <paramref name="date"/>'s month (in the lowest display mode), sliding when <paramref name="animated"/> and it is another page.</summary>
    public void GoToDate(DateTime date, bool animated = true) => _model.GoToDate(date, animated);

    /// <summary>Shows today's month.</summary>
    public void GoToToday(bool animated = true) => _model.GoToDate(DateTime.Today, animated);

    /// <summary>Clears the selection of every mode.</summary>
    public void ClearSelection()
    {
        if (_model.Clear() is { } args)
            SelectionDone(args);
    }

    /// <summary>Asks <see cref="DayStyle"/> again for every day (the app's data behind it changed).</summary>
    public void RefreshDayStyles()
    {
        _model.RefreshDayStyles();
        InvalidatePaint();
    }

    #endregion

    #region Display

    /// <summary>A date of the page shown (default today; its time is dropped). Kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>; set when the user moves the page.</summary>
    public DateTime DisplayDate { get => _model.DisplayDate; set => SetDisplayDate(value); }

    /// <summary>Sets <see cref="DisplayDate"/> (no slide; <see cref="GoToDate"/> slides).</summary>
    public SkUiCoreCalendar SetDisplayDate(DateTime value)
    {
        if (_model.SetDisplayDate(value))
            DisplayStateChanged();
        return this;
    }

    /// <summary>What the page shows: days (default), months, years or decades; kept within <see cref="MinimumDisplayMode"/> and <see cref="MaximumDisplayMode"/>.</summary>
    public SkUiCalendarDisplayMode DisplayMode { get => _model.DisplayMode; set => SetDisplayMode(value); }

    /// <summary>Sets <see cref="DisplayMode"/> (no zoom).</summary>
    public SkUiCoreCalendar SetDisplayMode(SkUiCalendarDisplayMode value)
    {
        ThrowIfUndefined(value);
        if (_model.SetDisplayMode(value))
            DisplayStateChanged();
        return this;
    }

    /// <summary>The lowest display mode (default <see cref="SkUiCalendarDisplayMode.Month"/>); <see cref="SkUiCalendarDisplayMode.Year"/> makes a month picker, whose month cells are chosen.</summary>
    public SkUiCalendarDisplayMode MinimumDisplayMode { get => _model.MinimumDisplayMode; set => SetMinimumDisplayMode(value); }

    /// <summary>Sets <see cref="MinimumDisplayMode"/>.</summary>
    public SkUiCoreCalendar SetMinimumDisplayMode(SkUiCalendarDisplayMode value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref _model.MinimumDisplayMode, value, nameof(MinimumDisplayMode)))
            ModeLimitsChanged();
        return this;
    }

    /// <summary>The highest display mode (default <see cref="SkUiCalendarDisplayMode.Century"/>); <see cref="SkUiCalendarDisplayMode.Month"/> keeps the calendar on days.</summary>
    public SkUiCalendarDisplayMode MaximumDisplayMode { get => _model.MaximumDisplayMode; set => SetMaximumDisplayMode(value); }

    /// <summary>Sets <see cref="MaximumDisplayMode"/>.</summary>
    public SkUiCoreCalendar SetMaximumDisplayMode(SkUiCalendarDisplayMode value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref _model.MaximumDisplayMode, value, nameof(MaximumDisplayMode)))
            ModeLimitsChanged();
        return this;
    }

    /// <summary>How many weeks a month page shows (1 to 6, default 6: the whole month); fewer pages by weeks (a weekly strip).</summary>
    public int VisibleWeeks { get => _model.VisibleWeeks; set => SetVisibleWeeks(value); }

    /// <summary>Sets <see cref="VisibleWeeks"/>.</summary>
    public SkUiCoreCalendar SetVisibleWeeks(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiCalendarGrid.MaxWeeks);
        if (SetProperty(ref _model.VisibleWeeks, value, nameof(VisibleWeeks)))
            PageShapeChanged(measure: true);
        return this;
    }

    /// <summary>The week's first day, its first column; <c>null</c> (default): the current culture's.</summary>
    public DayOfWeek? FirstDayOfWeek { get => _model.FirstDayOfWeek; set => SetFirstDayOfWeek(value); }

    /// <summary>Sets <see cref="FirstDayOfWeek"/>.</summary>
    public SkUiCoreCalendar SetFirstDayOfWeek(DayOfWeek? value)
    {
        if (value is { } day && !Enum.IsDefined(day))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (SetProperty(ref _model.FirstDayOfWeek, value, nameof(FirstDayOfWeek)))
            PageShapeChanged(measure: false);
        return this;
    }

    /// <summary>The direction a swipe moves between pages: horizontal (default) or vertical (the header buttons' chevrons then point up and down).</summary>
    public StackOrientation NavigationOrientation { get => _model.NavigationOrientation; set => SetNavigationOrientation(value); }

    /// <summary>Sets <see cref="NavigationOrientation"/>.</summary>
    public SkUiCoreCalendar SetNavigationOrientation(StackOrientation value)
    {
        SkUiValidate.ThrowIfNotOrientation(value, nameof(value));
        return SetPainted(ref _model.NavigationOrientation, value, nameof(NavigationOrientation)); // the buttons' chevrons follow it
    }

    /// <summary>Whether a swipe moves between pages (default <c>true</c>).</summary>
    public bool IsSwipeEnabled { get => _model.IsSwipeEnabled; set => SetIsSwipeEnabled(value); }

    /// <summary>Sets <see cref="IsSwipeEnabled"/>.</summary>
    public SkUiCoreCalendar SetIsSwipeEnabled(bool value)
    {
        SetProperty(ref _model.IsSwipeEnabled, value, nameof(IsSwipeEnabled));
        return this;
    }

    /// <summary>Whether a pinch zooms out (a display mode up) or in (default <c>true</c>).</summary>
    public bool IsPinchEnabled { get => _model.IsPinchEnabled; set => SetIsPinchEnabled(value); }

    /// <summary>Sets <see cref="IsPinchEnabled"/>.</summary>
    public SkUiCoreCalendar SetIsPinchEnabled(bool value)
    {
        SetProperty(ref _model.IsPinchEnabled, value, nameof(IsPinchEnabled));
        return this;
    }

    #endregion

    #region Header and day names

    /// <summary>Whether the header (title and previous / next buttons) shows (default <c>true</c>).</summary>
    public bool IsHeaderVisible { get => _model.IsHeaderVisible; set => SetIsHeaderVisible(value); }

    /// <summary>Sets <see cref="IsHeaderVisible"/>.</summary>
    public SkUiCoreCalendar SetIsHeaderVisible(bool value) => SetMeasured(ref _model.IsHeaderVisible, value, nameof(IsHeaderVisible));

    /// <summary>Where the header puts its title and buttons (default: the title first, both buttons at the end).</summary>
    public SkUiCalendarHeaderLayout HeaderLayout { get => _model.HeaderLayout; set => SetHeaderLayout(value); }

    /// <summary>Sets <see cref="HeaderLayout"/>.</summary>
    public SkUiCoreCalendar SetHeaderLayout(SkUiCalendarHeaderLayout value)
    {
        ThrowIfUndefined(value);
        return SetPainted(ref _model.HeaderLayout, value, nameof(HeaderLayout));
    }

    /// <summary>Whether the header's previous / next buttons show (default <c>true</c>).</summary>
    public bool AreNavigationButtonsVisible { get => _model.AreNavigationButtonsVisible; set => SetAreNavigationButtonsVisible(value); }

    /// <summary>Sets <see cref="AreNavigationButtonsVisible"/>.</summary>
    public SkUiCoreCalendar SetAreNavigationButtonsVisible(bool value) => SetPainted(ref _model.AreNavigationButtonsVisible, value, nameof(AreNavigationButtonsVisible));

    /// <summary>The title's .NET date format on month pages (<c>"MMM yyyy"</c>); <c>null</c> (default): the culture's year-month pattern.</summary>
    public string? TitleFormat { get => _model.TitleFormat; set => SetTitleFormat(value); }

    /// <summary>Sets <see cref="TitleFormat"/>.</summary>
    public SkUiCoreCalendar SetTitleFormat(string? value)
    {
        if (SetProperty(ref _model.TitleFormat, value, nameof(TitleFormat)))
        {
            InvalidatePaint();
            DisplayStateChanged();
        }
        return this;
    }

    /// <summary>The title's color; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? HeaderTextColor { get => _model.HeaderTextColor; set => SetHeaderTextColor(value); }

    /// <summary>Sets <see cref="HeaderTextColor"/>.</summary>
    public SkUiCoreCalendar SetHeaderTextColor(Color? value) => SetPainted(ref _model.HeaderTextColor, value, nameof(HeaderTextColor));

    /// <summary>The title's font size; NaN (default): the look's (<see cref="SkUiLook.CalendarHeaderFontSize"/>).</summary>
    public double HeaderFontSize { get => _model.HeaderFontSize; set => SetHeaderFontSize(value); }

    /// <summary>Sets <see cref="HeaderFontSize"/>.</summary>
    public SkUiCoreCalendar SetHeaderFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        return SetPainted(ref _model.HeaderFontSize, value, nameof(HeaderFontSize));
    }

    /// <summary>The title's font attributes (default bold).</summary>
    public FontAttributes HeaderFontAttributes { get => _model.HeaderFontAttributes; set => SetHeaderFontAttributes(value); }

    /// <summary>Sets <see cref="HeaderFontAttributes"/>.</summary>
    public SkUiCoreCalendar SetHeaderFontAttributes(FontAttributes value) => SetPainted(ref _model.HeaderFontAttributes, value, nameof(HeaderFontAttributes));

    /// <summary>The buttons' chevron color; <c>null</c>: the color scheme's accent.</summary>
    public Color? NavigationButtonColor { get => _model.NavigationButtonColor; set => SetNavigationButtonColor(value); }

    /// <summary>Sets <see cref="NavigationButtonColor"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonColor(Color? value) => SetPainted(ref _model.NavigationButtonColor, value, nameof(NavigationButtonColor));

    /// <summary>A circle behind each button; <c>null</c> (default): none.</summary>
    public Color? NavigationButtonBackgroundColor { get => _model.NavigationButtonBackgroundColor; set => SetNavigationButtonBackgroundColor(value); }

    /// <summary>Sets <see cref="NavigationButtonBackgroundColor"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonBackgroundColor(Color? value) =>
        SetPainted(ref _model.NavigationButtonBackgroundColor, value, nameof(NavigationButtonBackgroundColor));

    /// <summary>The side of the header's previous / next buttons (their touch target); negative (default -1): the look's (<see cref="SkUiLook.CalendarNavigationButtonSize"/>, 36). Kept within the header's height.</summary>
    public double NavigationButtonSize { get => _model.NavigationButtonSize; set => SetNavigationButtonSize(value); }

    /// <summary>Sets <see cref="NavigationButtonSize"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonSize(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetPainted(ref _model.NavigationButtonSize, value, nameof(NavigationButtonSize));
    }

    /// <summary>The gap between the two buttons when they sit together at the end, and between a button and the centered title; negative (default -1): 4.</summary>
    public double NavigationButtonSpacing { get => _model.NavigationButtonSpacing; set => SetNavigationButtonSpacing(value); }

    /// <summary>Sets <see cref="NavigationButtonSpacing"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonSpacing(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetPainted(ref _model.NavigationButtonSpacing, value, nameof(NavigationButtonSpacing));
    }

    /// <summary>The corner radius of the buttons' background and border; negative (default -1): a circle.</summary>
    public double NavigationButtonCornerRadius { get => _model.NavigationButtonCornerRadius; set => SetNavigationButtonCornerRadius(value); }

    /// <summary>Sets <see cref="NavigationButtonCornerRadius"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonCornerRadius(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetPainted(ref _model.NavigationButtonCornerRadius, value, nameof(NavigationButtonCornerRadius));
    }

    /// <summary>The buttons' border color; <c>null</c> (default): none.</summary>
    public Color? NavigationButtonBorderColor { get => _model.NavigationButtonBorderColor; set => SetNavigationButtonBorderColor(value); }

    /// <summary>Sets <see cref="NavigationButtonBorderColor"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonBorderColor(Color? value)
    => SetPainted(ref _model.NavigationButtonBorderColor, value, nameof(NavigationButtonBorderColor));

    /// <summary>The buttons' border width (default 0: none).</summary>
    public double NavigationButtonBorderWidth { get => _model.NavigationButtonBorderWidth; set => SetNavigationButtonBorderWidth(value); }

    /// <summary>Sets <see cref="NavigationButtonBorderWidth"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonBorderWidth(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetPainted(ref _model.NavigationButtonBorderWidth, value, nameof(NavigationButtonBorderWidth));
    }

    /// <summary>An icon for the previous button instead of the look's chevron: a glyph of <see cref="NavigationButtonFontFamily"/> (an icon font), in <see cref="NavigationButtonColor"/>; mirrored right-to-left. <c>null</c> (default): the chevron.</summary>
    public string? PreviousButtonGlyph { get => _model.PreviousButtonGlyph; set => SetPreviousButtonGlyph(value); }

    /// <summary>Sets <see cref="PreviousButtonGlyph"/>.</summary>
    public SkUiCoreCalendar SetPreviousButtonGlyph(string? value)
    => SetPainted(ref _model.PreviousButtonGlyph, value, nameof(PreviousButtonGlyph));

    /// <summary>An icon for the next button instead of the look's chevron (see <see cref="PreviousButtonGlyph"/>).</summary>
    public string? NextButtonGlyph { get => _model.NextButtonGlyph; set => SetNextButtonGlyph(value); }

    /// <summary>Sets <see cref="NextButtonGlyph"/>.</summary>
    public SkUiCoreCalendar SetNextButtonGlyph(string? value)
    => SetPainted(ref _model.NextButtonGlyph, value, nameof(NextButtonGlyph));

    /// <summary>An image for the previous button instead of the glyph or the look's chevron (a PNG, an SVG, a URL: any source the image loader reads), drawn at <see cref="NavigationButtonGlyphSize"/> and mirrored right-to-left; tint an SVG with <c>SkUiImageSource.FromVector(source, color)</c>. <c>null</c> (default): none.</summary>
    public SkUiImageSource? PreviousButtonIcon { get => _model.PreviousButtonIcon; set => SetPreviousButtonIcon(value); }

    /// <summary>Sets <see cref="PreviousButtonIcon"/> (a MAUI <c>ImageSource</c> or a file name converts).</summary>
    public SkUiCoreCalendar SetPreviousButtonIcon(SkUiImageSource? value)
    {
        if (Equals(value, _model.PreviousButtonIcon))
            return this;
        _model.PreviousButtonIcon = value;
        OnPropertyChanged(nameof(PreviousButtonIcon));
        InvalidatePaint();
        return this;
    }

    /// <summary>An image for the next button (see <see cref="PreviousButtonIcon"/>).</summary>
    public SkUiImageSource? NextButtonIcon { get => _model.NextButtonIcon; set => SetNextButtonIcon(value); }

    /// <summary>Sets <see cref="NextButtonIcon"/>.</summary>
    public SkUiCoreCalendar SetNextButtonIcon(SkUiImageSource? value)
    {
        if (Equals(value, _model.NextButtonIcon))
            return this;
        _model.NextButtonIcon = value;
        OnPropertyChanged(nameof(NextButtonIcon));
        InvalidatePaint();
        return this;
    }

    /// <summary>The font of <see cref="PreviousButtonGlyph"/> and <see cref="NextButtonGlyph"/> (an icon font registered with the app); <c>null</c>: <see cref="FontFamily"/>.</summary>
    public string? NavigationButtonFontFamily { get => _model.NavigationButtonFontFamily; set => SetNavigationButtonFontFamily(value); }

    /// <summary>Sets <see cref="NavigationButtonFontFamily"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonFontFamily(string? value)
    => SetPainted(ref _model.NavigationButtonFontFamily, value, nameof(NavigationButtonFontFamily));

    /// <summary>The glyphs' font size; NaN (default): half the button's side.</summary>
    public double NavigationButtonGlyphSize { get => _model.NavigationButtonGlyphSize; set => SetNavigationButtonGlyphSize(value); }

    /// <summary>Sets <see cref="NavigationButtonGlyphSize"/>.</summary>
    public SkUiCoreCalendar SetNavigationButtonGlyphSize(double value)
    {
        ThrowIfNotFontSize(value);
        return SetPainted(ref _model.NavigationButtonGlyphSize, value, nameof(NavigationButtonGlyphSize));
    }

    /// <summary>The title's font family; <c>null</c>: <see cref="FontFamily"/>.</summary>
    public string? HeaderFontFamily { get => _model.HeaderFontFamily; set => SetHeaderFontFamily(value); }

    /// <summary>Sets <see cref="HeaderFontFamily"/>.</summary>
    public SkUiCoreCalendar SetHeaderFontFamily(string? value)
    => SetPainted(ref _model.HeaderFontFamily, value, nameof(HeaderFontFamily));

    /// <summary>The header's height; negative (default -1): the look's (<see cref="SkUiLook.CalendarHeaderHeight"/>, 48).</summary>
    public double HeaderHeight { get => _model.HeaderHeight; set => SetHeaderHeight(value); }

    /// <summary>Sets <see cref="HeaderHeight"/>.</summary>
    public SkUiCoreCalendar SetHeaderHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetMeasured(ref _model.HeaderHeight, value, nameof(HeaderHeight));
    }

    /// <summary>The space inside the header around the title and the buttons (default 8 at the sides): line the title up with the days, or inset the buttons.</summary>
    public Thickness HeaderPadding { get => _model.HeaderPadding; set => SetHeaderPadding(value); }

    /// <summary>Sets <see cref="HeaderPadding"/>.</summary>
    public SkUiCoreCalendar SetHeaderPadding(Thickness value)
    {
        SkUiValidate.ThrowIfNotThickness(value, nameof(value));
        return SetPainted(ref _model.HeaderPadding, value, nameof(HeaderPadding));
    }

    /// <summary>Whether the row of day names shows above the days (default <c>true</c>).</summary>
    public bool AreDayNamesVisible { get => _model.AreDayNamesVisible; set => SetAreDayNamesVisible(value); }

    /// <summary>Sets <see cref="AreDayNamesVisible"/>.</summary>
    public SkUiCoreCalendar SetAreDayNamesVisible(bool value) => SetMeasured(ref _model.AreDayNamesVisible, value, nameof(AreDayNamesVisible));

    /// <summary>How the day names are written (default the culture's shortest names).</summary>
    public SkUiCalendarDayNameFormat DayNameFormat { get => _model.DayNameFormat; set => SetDayNameFormat(value); }

    /// <summary>Sets <see cref="DayNameFormat"/>.</summary>
    public SkUiCoreCalendar SetDayNameFormat(SkUiCalendarDayNameFormat value)
    {
        ThrowIfUndefined(value);
        return SetPainted(ref _model.DayNameFormat, value, nameof(DayNameFormat));
    }

    /// <summary>The day names' and week numbers' color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? DayNameTextColor { get => _model.DayNameTextColor; set => SetDayNameTextColor(value); }

    /// <summary>Sets <see cref="DayNameTextColor"/>.</summary>
    public SkUiCoreCalendar SetDayNameTextColor(Color? value) => SetPainted(ref _model.DayNameTextColor, value, nameof(DayNameTextColor));

    /// <summary>The day names' and week numbers' font size; NaN (default): the look's (<see cref="SkUiLook.CalendarDayNameFontSize"/>).</summary>
    public double DayNameFontSize { get => _model.DayNameFontSize; set => SetDayNameFontSize(value); }

    /// <summary>Sets <see cref="DayNameFontSize"/>.</summary>
    public SkUiCoreCalendar SetDayNameFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        return SetPainted(ref _model.DayNameFontSize, value, nameof(DayNameFontSize));
    }

    /// <summary>The day names' font attributes (default none).</summary>
    public FontAttributes DayNameFontAttributes { get => _model.DayNameFontAttributes; set => SetDayNameFontAttributes(value); }

    /// <summary>Sets <see cref="DayNameFontAttributes"/>.</summary>
    public SkUiCoreCalendar SetDayNameFontAttributes(FontAttributes value) => SetPainted(ref _model.DayNameFontAttributes, value, nameof(DayNameFontAttributes));

    /// <summary>Whether today's day name is drawn in <see cref="TodayDayNameTextColor"/> while the page shows today (default <c>true</c>).</summary>
    public bool IsTodayDayNameHighlighted { get => _model.IsTodayDayNameHighlighted; set => SetIsTodayDayNameHighlighted(value); }

    /// <summary>Sets <see cref="IsTodayDayNameHighlighted"/>.</summary>
    public SkUiCoreCalendar SetIsTodayDayNameHighlighted(bool value) => SetPainted(ref _model.IsTodayDayNameHighlighted, value, nameof(IsTodayDayNameHighlighted));

    /// <summary>Today's day name color; <c>null</c>: <see cref="TodayColor"/> (the color scheme's accent).</summary>
    public Color? TodayDayNameTextColor { get => _model.TodayDayNameTextColor; set => SetTodayDayNameTextColor(value); }

    /// <summary>Sets <see cref="TodayDayNameTextColor"/>.</summary>
    public SkUiCoreCalendar SetTodayDayNameTextColor(Color? value) => SetPainted(ref _model.TodayDayNameTextColor, value, nameof(TodayDayNameTextColor));

    #endregion

    #region Days

    /// <summary>Whether the neighbor months' days fill the first and last weeks (default <c>true</c>; a tap on one shows its month). Also the first and last cells of decade and century pages.</summary>
    public bool AreAdjacentDaysVisible { get => _model.AreAdjacentDaysVisible; set => SetAreAdjacentDaysVisible(value); }

    /// <summary>Sets <see cref="AreAdjacentDaysVisible"/>.</summary>
    public SkUiCoreCalendar SetAreAdjacentDaysVisible(bool value) => SetPainted(ref _model.AreAdjacentDaysVisible, value, nameof(AreAdjacentDaysVisible));

    /// <summary>Whether a column of week numbers shows at the start (default <c>false</c>), by the culture's week rule (ISO 8601 for Monday weeks).</summary>
    public bool AreWeekNumbersVisible { get => _model.AreWeekNumbersVisible; set => SetAreWeekNumbersVisible(value); }

    /// <summary>Sets <see cref="AreWeekNumbersVisible"/>.</summary>
    public SkUiCoreCalendar SetAreWeekNumbersVisible(bool value) => SetMeasured(ref _model.AreWeekNumbersVisible, value, nameof(AreWeekNumbersVisible));

    /// <summary>The days', months' and years' color; <c>null</c>: the color scheme's foreground.</summary>
    public Color? TextColor { get => _model.TextColor; set => SetTextColor(value); }

    /// <summary>Sets <see cref="TextColor"/>.</summary>
    public SkUiCoreCalendar SetTextColor(Color? value) => SetPainted(ref _model.TextColor, value, nameof(TextColor));

    /// <summary>The font family of every text of the calendar; <c>null</c>: the look's.</summary>
    public string? FontFamily { get => _model.FontFamily; set => SetFontFamily(value); }

    /// <summary>Sets <see cref="FontFamily"/>.</summary>
    public SkUiCoreCalendar SetFontFamily(string? value) => SetPainted(ref _model.FontFamily, value, nameof(FontFamily));

    /// <summary>The days', months' and years' font size; NaN (default): the look's (<see cref="SkUiLook.CalendarFontSize"/>).</summary>
    public double FontSize { get => _model.FontSize; set => SetFontSize(value); }

    /// <summary>Sets <see cref="FontSize"/>.</summary>
    public SkUiCoreCalendar SetFontSize(double value)
    {
        ThrowIfNotFontSize(value);
        return SetPainted(ref _model.FontSize, value, nameof(FontSize));
    }

    /// <summary>The days', months' and years' font attributes (default none; today has <see cref="TodayFontAttributes"/>).</summary>
    public FontAttributes FontAttributes { get => _model.FontAttributes; set => SetFontAttributes(value); }

    /// <summary>Sets <see cref="FontAttributes"/>.</summary>
    public SkUiCoreCalendar SetFontAttributes(FontAttributes value) => SetPainted(ref _model.FontAttributes, value, nameof(FontAttributes));

    /// <summary>Whether the texts follow the system's text size (default <c>true</c>).</summary>
    public bool FontAutoScalingEnabled { get => _model.FontAutoScalingEnabled; set => SetFontAutoScalingEnabled(value); }

    /// <summary>Sets <see cref="FontAutoScalingEnabled"/>.</summary>
    public SkUiCoreCalendar SetFontAutoScalingEnabled(bool value) => SetPainted(ref _model.FontAutoScalingEnabled, value, nameof(FontAutoScalingEnabled));

    /// <summary>A week row's height when the calendar sizes itself; negative (default -1): the look's (<see cref="SkUiLook.CalendarRowHeight"/>). Rows stretch to a taller calendar.</summary>
    public double DayRowHeight { get => _model.DayRowHeight; set => SetDayRowHeight(value); }

    /// <summary>Sets <see cref="DayRowHeight"/>.</summary>
    public SkUiCoreCalendar SetDayRowHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetMeasured(ref _model.DayRowHeight, value, nameof(DayRowHeight));
    }

    /// <summary>How today is marked (default a ring).</summary>
    public SkUiCalendarTodayStyle TodayStyle { get => _model.TodayStyle; set => SetTodayStyle(value); }

    /// <summary>Sets <see cref="TodayStyle"/>.</summary>
    public SkUiCoreCalendar SetTodayStyle(SkUiCalendarTodayStyle value)
    {
        ThrowIfUndefined(value);
        return SetPainted(ref _model.TodayStyle, value, nameof(TodayStyle));
    }

    /// <summary>Today's ring, fill or text color; <c>null</c>: the color scheme's accent.</summary>
    public Color? TodayColor { get => _model.TodayColor; set => SetTodayColor(value); }

    /// <summary>Sets <see cref="TodayColor"/>.</summary>
    public SkUiCoreCalendar SetTodayColor(Color? value) => SetPainted(ref _model.TodayColor, value, nameof(TodayColor));

    /// <summary>Today's number color; <c>null</c>: <see cref="TextColor"/> (a contrasting color when filled, <see cref="TodayColor"/> for the text style).</summary>
    public Color? TodayTextColor { get => _model.TodayTextColor; set => SetTodayTextColor(value); }

    /// <summary>Sets <see cref="TodayTextColor"/>.</summary>
    public SkUiCoreCalendar SetTodayTextColor(Color? value) => SetPainted(ref _model.TodayTextColor, value, nameof(TodayTextColor));

    /// <summary>Today's font attributes (default bold).</summary>
    public FontAttributes TodayFontAttributes { get => _model.TodayFontAttributes; set => SetTodayFontAttributes(value); }

    /// <summary>Sets <see cref="TodayFontAttributes"/>.</summary>
    public SkUiCoreCalendar SetTodayFontAttributes(FontAttributes value) => SetPainted(ref _model.TodayFontAttributes, value, nameof(TodayFontAttributes));

    /// <summary>The shape behind a selected day (default a circle).</summary>
    public SkUiCalendarSelectionShape SelectionShape { get => _model.SelectionShape; set => SetSelectionShape(value); }

    /// <summary>Sets <see cref="SelectionShape"/>.</summary>
    public SkUiCoreCalendar SetSelectionShape(SkUiCalendarSelectionShape value)
    {
        ThrowIfUndefined(value);
        return SetPainted(ref _model.SelectionShape, value, nameof(SelectionShape));
    }

    /// <summary>The selected days' fill (and a range's ends); <c>null</c>: the color scheme's accent.</summary>
    public Color? SelectionColor { get => _model.SelectionColor; set => SetSelectionColor(value); }

    /// <summary>Sets <see cref="SelectionColor"/>.</summary>
    public SkUiCoreCalendar SetSelectionColor(Color? value) => SetPainted(ref _model.SelectionColor, value, nameof(SelectionColor));

    /// <summary>The selected days' number color; <c>null</c>: white or near black, whichever reads on <see cref="SelectionColor"/>.</summary>
    public Color? SelectedTextColor { get => _model.SelectedTextColor; set => SetSelectedTextColor(value); }

    /// <summary>Sets <see cref="SelectedTextColor"/>.</summary>
    public SkUiCoreCalendar SetSelectedTextColor(Color? value) => SetPainted(ref _model.SelectedTextColor, value, nameof(SelectedTextColor));

    /// <summary>The band between a range's first and last days; <c>null</c>: <see cref="SelectionColor"/>, light.</summary>
    public Color? RangeColor { get => _model.RangeColor; set => SetRangeColor(value); }

    /// <summary>Sets <see cref="RangeColor"/>.</summary>
    public SkUiCoreCalendar SetRangeColor(Color? value) => SetPainted(ref _model.RangeColor, value, nameof(RangeColor));

    /// <summary>The number color of the days between a range's ends; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? RangeTextColor { get => _model.RangeTextColor; set => SetRangeTextColor(value); }

    /// <summary>Sets <see cref="RangeTextColor"/>.</summary>
    public SkUiCoreCalendar SetRangeTextColor(Color? value) => SetPainted(ref _model.RangeTextColor, value, nameof(RangeTextColor));

    /// <summary>The neighbor months' days' color; <c>null</c>: the color scheme's muted color.</summary>
    public Color? AdjacentDayTextColor { get => _model.AdjacentDayTextColor; set => SetAdjacentDayTextColor(value); }

    /// <summary>Sets <see cref="AdjacentDayTextColor"/>.</summary>
    public SkUiCoreCalendar SetAdjacentDayTextColor(Color? value) => SetPainted(ref _model.AdjacentDayTextColor, value, nameof(AdjacentDayTextColor));

    /// <summary>The color of days that cannot be chosen; <c>null</c>: the color scheme's disabled color.</summary>
    public Color? DisabledDayTextColor { get => _model.DisabledDayTextColor; set => SetDisabledDayTextColor(value); }

    /// <summary>Sets <see cref="DisabledDayTextColor"/>.</summary>
    public SkUiCoreCalendar SetDisabledDayTextColor(Color? value) => SetPainted(ref _model.DisabledDayTextColor, value, nameof(DisabledDayTextColor));

    /// <summary>Saturdays' and Sundays' color; <c>null</c>: <see cref="TextColor"/>.</summary>
    public Color? WeekendTextColor { get => _model.WeekendTextColor; set => SetWeekendTextColor(value); }

    /// <summary>Sets <see cref="WeekendTextColor"/>.</summary>
    public SkUiCoreCalendar SetWeekendTextColor(Color? value) => SetPainted(ref _model.WeekendTextColor, value, nameof(WeekendTextColor));

    /// <summary>
    /// The app's style per day (holidays, events as marker dots, cancelled days struck through); <c>null</c> (default): none.
    /// Asked once per day while drawing and remembered: call <see cref="RefreshDayStyles"/> when the data behind it changes.
    /// </summary>
    public Func<DateTime, SkUiCalendarDayStyle?>? DayStyle
    {
        get => _model.DayStyle;
        set => SetDayStyle(value);
    }

    /// <summary>
    /// The height of the area under each day's number for its markers (or a day painter's use); negative (default -1): the
    /// look's (<see cref="SkUiLook.CalendarMarkerAreaHeight"/>) when <see cref="DayStyle"/> or <see cref="DayPainter"/> is set,
    /// else none. Rows grow by it when the calendar sizes itself.
    /// </summary>
    public double DayMarkerAreaHeight { get => _model.DayMarkerAreaHeight; set => SetDayMarkerAreaHeight(value); }

    /// <summary>Sets <see cref="DayMarkerAreaHeight"/>.</summary>
    public SkUiCoreCalendar SetDayMarkerAreaHeight(double value)
    {
        SkUiValidate.ThrowIfNotFinite(value, nameof(value));
        return SetMeasured(ref _model.DayMarkerAreaHeight, value, nameof(DayMarkerAreaHeight));
    }

    /// <summary>
    /// Draws each day the app's way (events, avatars, prices), after the look's selection and today: the painter gets the
    /// day's parts, state and colors and can draw text through the calendar. Returning <c>true</c> skips the calendar's number
    /// and markers for that day. <c>null</c> (default): none.
    /// </summary>
    public SkUiCalendarDayPainter? DayPainter { get => _model.DayPainter; set => SetDayPainter(value); }

    /// <summary>Sets <see cref="DayPainter"/>.</summary>
    public SkUiCoreCalendar SetDayPainter(SkUiCalendarDayPainter? value)
    {
        if (ReferenceEquals(value, _model.DayPainter))
            return this;
        _model.DayPainter = value;
        OnPropertyChanged(nameof(DayPainter));
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="DayStyle"/>.</summary>
    public SkUiCoreCalendar SetDayStyle(Func<DateTime, SkUiCalendarDayStyle?>? value)
    {
        if (ReferenceEquals(value, _model.DayStyle))
            return this;
        _model.DayStyle = value;
        OnPropertyChanged(nameof(DayStyle));
        InvalidateMeasure(); // the marker area comes and goes with it
        return this;
    }

    #endregion

    #region Selection and rules

    /// <summary>How many days the user can choose (default one).</summary>
    public SkUiCalendarSelectionMode SelectionMode { get => _model.SelectionMode; set => SetSelectionMode(value); }

    /// <summary>Sets <see cref="SelectionMode"/>.</summary>
    public SkUiCoreCalendar SetSelectionMode(SkUiCalendarSelectionMode value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref _model.SelectionMode, value, nameof(SelectionMode)))
        {
            InvalidatePaint();
            InvalidateSemantics();
        }
        return this;
    }

    /// <summary>
    /// Which end of the range a tap sets (default <see cref="SkUiCalendarRangeTarget.Auto"/>: the first tap starts a range, the
    /// second ends it). <see cref="SkUiCalendarRangeTarget.Start"/> / <see cref="SkUiCalendarRangeTarget.End"/> let the app (a
    /// "start" and an "end" field above the calendar) say which; after a tap on the start the calendar moves it to the end.
    /// </summary>
    public SkUiCalendarRangeTarget RangeSelectionTarget { get => _model.RangeSelectionTarget; set => SetRangeSelectionTarget(value); }

    /// <summary>Sets <see cref="RangeSelectionTarget"/>.</summary>
    public SkUiCoreCalendar SetRangeSelectionTarget(SkUiCalendarRangeTarget value)
    {
        ThrowIfUndefined(value);
        if (SetProperty(ref _model.RangeSelectionTarget, value, nameof(RangeSelectionTarget)))
            _reportedTarget = value;
        return this;
    }

    /// <summary>Whether a tap on the selected day clears the single selection (default <c>false</c>). Multiple selection always toggles.</summary>
    public bool AllowsDeselection { get => _model.AllowsDeselection; set => SetAllowsDeselection(value); }

    /// <summary>Sets <see cref="AllowsDeselection"/>.</summary>
    public SkUiCoreCalendar SetAllowsDeselection(bool value)
    {
        SetProperty(ref _model.AllowsDeselection, value, nameof(AllowsDeselection));
        return this;
    }

    /// <summary>The chosen day (single selection), or the last day chosen (multiple selection, a range's first day); <c>null</c>: none.</summary>
    public DateTime? SelectedDate { get => _model.SelectedDate; set => SetSelectedDate(value); }

    /// <summary>Sets <see cref="SelectedDate"/> (its time is dropped).</summary>
    public SkUiCoreCalendar SetSelectedDate(DateTime? value)
    {
        var before = _model.SelectedDate;
        var args = _model.SetSelectedDate(value);
        if (before != _model.SelectedDate)
            OnPropertyChanged(nameof(SelectedDate));
        if (args is not null)
            SelectionDone(args, raiseProperties: false);
        return this;
    }

    /// <summary>The chosen days of multiple selection, in date order (a new list per change; empty by default).</summary>
    public IReadOnlyList<DateTime> SelectedDates { get => _model.SelectedDates; set => SetSelectedDates(value); }

    /// <summary>Sets <see cref="SelectedDates"/> (times dropped, duplicates removed).</summary>
    public SkUiCoreCalendar SetSelectedDates(IEnumerable<DateTime>? value)
    {
        if (_model.SetSelectedDates(value) is { } args)
        {
            OnPropertyChanged(nameof(SelectedDates));
            SelectionDone(args, raiseProperties: false);
        }
        return this;
    }

    /// <summary>The chosen range of range selection; <c>null</c>: none. After the first tap it is open (its <see cref="SkUiDateRange.End"/> <c>null</c>), after the second complete.</summary>
    public SkUiDateRange? SelectedRange { get => _model.SelectedRange; set => SetSelectedRange(value); }

    /// <summary>Sets <see cref="SelectedRange"/>.</summary>
    public SkUiCoreCalendar SetSelectedRange(SkUiDateRange? value)
    {
        if (_model.SetSelectedRange(value) is { } args)
        {
            OnPropertyChanged(nameof(SelectedRange));
            SelectionDone(args, raiseProperties: false);
        }
        return this;
    }

    /// <inheritdoc cref="SkUiCalendar.SelectedRanges" />
    public IReadOnlyList<SkUiDateRange> SelectedRanges { get => _model.SelectedRanges; set => SetSelectedRanges(value); }

    /// <summary>Sets <see cref="SelectedRanges"/> (ordered, ranges that overlap or touch joined).</summary>
    public SkUiCoreCalendar SetSelectedRanges(IEnumerable<SkUiDateRange>? value)
    {
        var range = _model.SelectedRange;
        if (_model.SetSelectedRanges(value) is { } args)
        {
            OnPropertyChanged(nameof(SelectedRanges));
            if (!Nullable.Equals(range, _model.SelectedRange))
                OnPropertyChanged(nameof(SelectedRange));
            SelectionDone(args, raiseProperties: false);
        }
        return this;
    }

    /// <inheritdoc cref="SkUiCalendar.MinimumRangeDays" />
    public int MinimumRangeDays { get => _model.MinimumRangeDays; set => SetMinimumRangeDays(value); }

    /// <summary>Sets <see cref="MinimumRangeDays"/>.</summary>
    public SkUiCoreCalendar SetMinimumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (SetProperty(ref _model.MinimumRangeDays, value, nameof(MinimumRangeDays)))
        {
            InvalidatePaint();
            InvalidateSemantics();
        }
        return this;
    }

    /// <inheritdoc cref="SkUiCalendar.MaximumRangeDays" />
    public int MaximumRangeDays { get => _model.MaximumRangeDays; set => SetMaximumRangeDays(value); }

    /// <summary>Sets <see cref="MaximumRangeDays"/>.</summary>
    public SkUiCoreCalendar SetMaximumRangeDays(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (SetProperty(ref _model.MaximumRangeDays, value, nameof(MaximumRangeDays)))
        {
            InvalidatePaint();
            InvalidateSemantics();
        }
        return this;
    }

    /// <inheritdoc cref="SkUiCalendar.AllowsDisabledDaysInRange" />
    public bool AllowsDisabledDaysInRange { get => _model.AllowsDisabledDaysInRange; set => SetAllowsDisabledDaysInRange(value); }

    /// <summary>Sets <see cref="AllowsDisabledDaysInRange"/>.</summary>
    public SkUiCoreCalendar SetAllowsDisabledDaysInRange(bool value)
    {
        if (SetProperty(ref _model.AllowsDisabledDaysInRange, value, nameof(AllowsDisabledDaysInRange)))
        {
            InvalidatePaint();
            InvalidateSemantics();
        }
        return this;
    }

    /// <summary>The earliest day that can be chosen and shown; <c>null</c> (default): none.</summary>
    public DateTime? MinimumDate { get => _model.MinimumDate; set => SetMinimumDate(value); }

    /// <summary>Sets <see cref="MinimumDate"/>.</summary>
    public SkUiCoreCalendar SetMinimumDate(DateTime? value)
    {
        if (SetProperty(ref _model.MinimumDate, value?.Date, nameof(MinimumDate)))
            RangeChanged();
        return this;
    }

    /// <summary>The latest day that can be chosen and shown; <c>null</c> (default): none.</summary>
    public DateTime? MaximumDate { get => _model.MaximumDate; set => SetMaximumDate(value); }

    /// <summary>Sets <see cref="MaximumDate"/>.</summary>
    public SkUiCoreCalendar SetMaximumDate(DateTime? value)
    {
        if (SetProperty(ref _model.MaximumDate, value?.Date, nameof(MaximumDate)))
            RangeChanged();
        return this;
    }

    /// <summary>Days that cannot be chosen (holidays, booked days), dimmed. An empty list by default: add to it, or set your own (an observable list is followed).</summary>
    public IList<DateTime> BlackoutDates { get => _model.BlackoutDates!; set => SetBlackoutDates(value); }

    /// <summary>Sets <see cref="BlackoutDates"/>.</summary>
    public SkUiCoreCalendar SetBlackoutDates(IList<DateTime> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (ReferenceEquals(value, _model.BlackoutDates))
            return this;
        _model.BlackoutDates = value;
        _blackoutListener.Listen(value);
        OnPropertyChanged(nameof(BlackoutDates));
        InvalidatePaint();
        return this;
    }

    /// <summary>Whether a day can be chosen, a rule of the app's (weekdays only); <c>null</c> (default): every day in the range.</summary>
    public Func<DateTime, bool>? DateEnabled { get => _model.DateEnabled; set => SetDateEnabled(value); }

    /// <summary>Sets <see cref="DateEnabled"/>.</summary>
    public SkUiCoreCalendar SetDateEnabled(Func<DateTime, bool>? value)
    {
        if (SetProperty(ref _model.DateEnabled, value, nameof(DateEnabled)))
            InvalidatePaint();
        return this;
    }

    #endregion

    #region Helpers

    private static void ThrowIfUndefined<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void ThrowIfNotFontSize(double value)
    {
        if (!double.IsNaN(value))
            SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
    }

    private SkUiCoreCalendar SetPainted<T>(ref T field, T value, string name)
    {
        if (SetProperty(ref field, value, name))
            InvalidatePaint();
        return this;
    }

    private SkUiCoreCalendar SetMeasured<T>(ref T field, T value, string name)
    {
        if (SetProperty(ref field, value, name))
            InvalidateMeasure();
        return this;
    }

    private void ModeLimitsChanged()
    {
        _model.Revalidate();
        DisplayStateChanged();
        InvalidatePaint();
    }

    private void PageShapeChanged(bool measure)
    {
        _model.Revalidate();
        DisplayStateChanged();
        if (measure)
            InvalidateMeasure();
        else
            InvalidatePaint();
    }

    private void RangeChanged()
    {
        _model.SetDisplayDate(_model.DisplayDate);
        DisplayStateChanged();
        InvalidatePaint();
    }

    private void OnBlackoutDatesChanged()
    {
        _model.BlackoutDatesChanged();
        InvalidatePaint();
    }

    /// <summary>Raises the display properties that changed since they were last reported.</summary>
    private void DisplayStateChanged()
    {
        if (_reportedDisplay != _model.DisplayDate)
        {
            _reportedDisplay = _model.DisplayDate;
            OnPropertyChanged(nameof(DisplayDate));
        }
        if (_reportedMode != _model.DisplayMode)
        {
            _reportedMode = _model.DisplayMode;
            OnPropertyChanged(nameof(DisplayMode));
        }
        var title = _model.Title;
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

    private void SelectionDone(SkUiCalendarSelectionChangedEventArgs args, bool raiseProperties = true)
    {
        if (raiseProperties)
        {
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(SelectedDates));
            OnPropertyChanged(nameof(SelectedRange));
            OnPropertyChanged(nameof(SelectedRanges));
        }
        if (_reportedTarget != _model.RangeSelectionTarget)
        {
            _reportedTarget = _model.RangeSelectionTarget;
            OnPropertyChanged(nameof(RangeSelectionTarget));
        }
        InvalidatePaint();
        InvalidateSemantics();
        SelectionChanged?.Invoke(this, args);
    }

    #endregion

    #region Engine host, drawing and input

    bool ISkUiCalendarHost.CalendarRightToLeft => IsRightToLeft;

    bool ISkUiCalendarHost.CalendarEnabled => IsEnabled;

    Size ISkUiCalendarHost.CalendarSize => Frame.Size;

    void ISkUiCalendarHost.CalendarInvalidate(bool measure)
    {
        if (measure)
            InvalidateMeasure();
        else
            InvalidatePaint();
    }

    void ISkUiCalendarHost.CalendarDisplayChanged() => DisplayStateChanged();

    void ISkUiCalendarHost.CalendarSelectionChanged(SkUiCalendarSelectionChangedEventArgs args) => SelectionDone(args);

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
        _model.DrawCustomText(canvas, text, bounds, color, fontSize, attributes, fontFamily, alignment);

    bool ISkUiCalendarHost.CalendarDrawNavigationButton(SKCanvas canvas, in SkUiCalendarNavigationButtonPaint button, bool isNext) =>
        OnDrawNavigationButton(canvas, button, isNext);

    bool ISkUiCalendarHost.CalendarDrawTitle(SKCanvas canvas, SKRect bounds, string title, SKColor color, bool isPressed) =>
        OnDrawTitle(canvas, bounds, title, color, isPressed);


    /// <summary>The engine (tests).</summary>
    internal SkUiCalendarModel Model => _model;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _model.Measure(widthConstraint, heightConstraint);

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => _model.Draw(canvas, (float)Frame.Width, (float)Frame.Height);

    /// <inheritdoc />
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        recognizers.Add(_gesture ??= new SkUiCalendarGestureRecognizer(_model, () => Frame.Size, () => _pinch?.HasTwoPointers == true));
        recognizers.Add(_pinch ??= new SkUiPinchGestureRecognizer
        {
            Handler = args =>
            {
                if (args.Status == GestureStatus.Completed)
                    _model.Pinch(args.TotalScale, args.Origin, (float)Frame.Width, (float)Frame.Height);
            }
        });
    }

    bool ISkUiKeyHandler.OnKeyDown(in SkUiKeyEvent key) => _model.OnKey(key);

    /// <inheritdoc />
    internal override bool TakesKeyboardFocus => true;

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        _model.PopulateSemantics(info);
    }

    /// <inheritdoc />
    protected override bool OnSemanticsAction(SkUiSemanticsActions action) => _model.OnSemanticsAction(action) || base.OnSemanticsAction(action);

    /// <inheritdoc />
    internal override bool OnSemanticsPartAction(int part, SkUiSemanticsActions action) => _model.OnSemanticsPartAction(part, action);

    /// <inheritdoc />
    protected override void OnIsEnabledChanged()
    {
        base.OnIsEnabledChanged();
        InvalidatePaint();
    }

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
        _model.Release();
    }

    #endregion
}
