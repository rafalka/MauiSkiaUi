using System.Collections.ObjectModel;
using System.Globalization;
using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls.TimeClock;

/// <summary>
/// HOWTO: the shifts and leave screens of an employee self-service app, in one <see cref="SkUiTabView"/>:
/// <list type="bullet">
/// <item><b>Tab headers of the app's own</b> (<see cref="SkUiTabView.HeaderTemplate"/>): a title and a count pill bound to the tab item
/// (<c>Header</c>, <c>BadgeText</c>, and the read-only <c>IsSelected</c> and <c>CurrentTextColor</c> the tab view keeps up to date).
/// The counts change as shifts are dropped and claimed.</item>
/// <item><b>My shifts:</b> filter chips in one scrolling row (<see cref="SkUiChipGroup"/>, <c>Wraps = false</c>) over a list grouped by week.
/// Each row is an <see cref="SkUiSwipeView"/> with "Swap" and "Drop" behind it; the rows share an <see cref="SkUiSwipeView.ExclusiveGroup"/>,
/// so opening one closes the other, and changing the filters closes every open row (<see cref="SkUiCollectionView.CloseSwipeViews"/>).
/// Screen readers reach both actions as the row's custom actions. "Drop" asks in a dialog first.</item>
/// <item><b>Open shifts:</b> each has a "Claim" chip (an action chip that shrinks while pressed, <see cref="SkUiView.PressedScale"/>);
/// a claim moves the shift to My shifts and is announced (<see cref="SkUiAccessibility.Announce(SkUiView, string, SkUiLiveRegion)"/>).</item>
/// <item><b>Leave:</b> a progress ring per balance with the days left inside, a stacked bar of the year's vacation (taken, requested,
/// available), and a "Request leave" form in an <see cref="SkUiSheet"/> placed on the page. The form's date range picker opens its
/// calendar in a sheet over the form (<see cref="SkUiSheetPopupPresenter"/>). Sending a request tweens the bar and the ring.</item>
/// </list>
/// </summary>
public sealed class ShiftsAndLeaveSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Shifts and leave (tabs, chips, swipe rows, sheet)",
        Summary: "An employee's shifts and leave in a tab view with headers of the app's own: filter chips over swipeable shift rows that " +
                 "open one at a time, open shifts claimed with a chip, and leave balances as rings and a stacked bar, with a request " +
                 "form in a bottom sheet.",
        HowTo:
        [
            "Give every tab a header view with `tabs.HeaderTemplate = new DataTemplate(…)`: its binding context is the tab item, so bind " +
                "`Header`, `BadgeText`, `IsSelected` and `CurrentTextColor` (the look's text color for the tab's state).",
            "Filter with `SkUiChipGroup` (`ItemsSource`, `SelectionMode = Multiple`, `SelectionChanged`); `Wraps = false` and `Padding` " +
                "make one row that scrolls to the screen's edges.",
            "Let swipe rows open one at a time with the same `ExclusiveGroup` on each `SkUiSwipeView`, and close them all with " +
                "`collectionView.CloseSwipeViews()` when the list changes under them.",
            "Place an `SkUiSheet` as the last child of the grid that fills the page, and open it with `IsOpen = true`; " +
                "`Detents = [SkUiSheetDetent.FitContent]` makes it as tall as the form, `Close()` closes it.",
            "Open a picker's popup in a sheet with `PopupPresenter = new SkUiSheetPopupPresenter()` (or for the whole app, " +
                "`SkUiPopups.Presenter`): it opens over the sheet it came from."
        ],
        ThingsToKnow:
        [
            "A header view takes no input of its own: a tap anywhere on it selects the tab, and screen readers read the tab's `Header`.",
            "A placed sheet covers the layout it is placed in, so it goes in the page around the tab view, not in a tab's pane.",
            "Swipe items in `Reveal` mode stay open until tapped or closed; `Execute` mode acts at once (the Contacts example).",
            "A group's chips take their item as binding context; for chips with badges or images of their own, build `SkUiChip`s " +
                "yourself, as the Claim chips here.",
            "A stacked bar and a ring tween value changes with the look's transitions; under reduce motion they jump."
        ]);

    private const string Morning = "Morning";
    private const string Late = "Late";
    private const string Night = "Night";
    private const string Weekend = "Weekend";
    private const string Vacation = "Vacation";
    private const string Sick = "Sick leave";
    private const string Personal = "Personal";
    private const string Pending = "Pending";
    private const string Approved = "Approved";
    private const string ShiftRowsGroup = "MyShifts";

    private static readonly Color VacationColor = SampleColors.Accent;
    private static readonly Color RequestedColor = Color.FromArgb("#F59E0B");
    private static readonly Color TakenColor = Color.FromArgb("#94A3B8");
    private static readonly Color SickColor = Color.FromArgb("#2563EB");
    private static readonly Color PersonalColor = Color.FromArgb("#9333EA");
    private static readonly Color SwapColor = Color.FromArgb("#2563EB");
    private static readonly Color DropColor = Color.FromArgb("#C62828");
    private static readonly Color TrackColor = Color.FromArgb("#E6ECEF");
    private static readonly SkUiFontFeatures Tabular = [SkUiFontFeature.TabularFigures];

    /// <summary>A shift: when, how long, what and where; <see cref="SwapRequested"/> once the user asked to swap it.</summary>
    public sealed class Shift(DateTime start, TimeSpan length, string role, string place)
    {
        public DateTime Start { get; } = start;
        public TimeSpan Length { get; } = length;
        public string Role { get; } = role;
        public string Place { get; } = place;
        public bool SwapRequested { get; set; }

        public string Kind => Start.Hour < 12 ? Morning : Start.Hour < 20 ? Late : Night;
        public bool IsWeekend => Start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        public string Times =>
            $"{Start.ToString("t", CultureInfo.CurrentCulture)} – {(Start + Length).ToString("t", CultureInfo.CurrentCulture)}";
    }

    /// <summary>A week of shifts: a group of the list, with its header's text.</summary>
    public sealed class ShiftWeek(string name, IEnumerable<Shift> shifts) : List<Shift>(shifts)
    {
        public string Name { get; } = name;
    }

    private sealed record LeaveRequest(SkUiDateRange Range, string Type, int Days, string Status);

    private sealed class Balance(string type, Color color, int allowance, int taken)
    {
        public string Type { get; } = type;
        public Color Color { get; } = color;
        public int Allowance { get; } = allowance;
        public int Taken { get; } = taken;
        public int Requested { get; set; }
        public int Left => Allowance - Taken - Requested;
    }

    private readonly List<Shift> _myShifts = [];
    private readonly List<Shift> _openShifts = [];
    private readonly ObservableCollection<object> _filters = [];
    private readonly List<LeaveRequest> _requests = [];
    private readonly Balance[] _balances =
    [
        new(Vacation, VacationColor, 26, 9),
        new(Sick, SickColor, 10, 2),
        new(Personal, PersonalColor, 3, 1)
    ];

    private readonly SkUiTabView _tabs = new();
    private readonly SkUiTabItem _myShiftsTab = new() { Header = "My shifts" };
    private readonly SkUiTabItem _openShiftsTab = new() { Header = "Open" };
    private readonly SkUiTabItem _leaveTab = new() { Header = "Leave" };
    private readonly SkUiCollectionView _list;
    private readonly SkUiLabel _summary = new() { FontSize = 13, TextColor = SampleColors.Caption, FontFeatures = Tabular, Margin = new Thickness(16, 0) };
    private readonly SkUiVerticalStackLayout _open = new() { Spacing = 10, Padding = new Thickness(16) };
    private readonly SkUiProgressRing[] _rings = new SkUiProgressRing[3];
    private readonly SkUiLabel[] _ringValues = new SkUiLabel[3];
    private readonly SkUiBarSegment _vacationTaken = new(0, TakenColor, "Taken");
    private readonly SkUiBarSegment _vacationRequested = new(0, RequestedColor, "Requested");
    private readonly SkUiBarSegment _vacationLeft = new(0, VacationColor, "Available");
    private readonly SkUiLabel _vacationLegend = new() { FontSize = 13, TextColor = SampleColors.Caption, FontFeatures = Tabular };
    private readonly SkUiVerticalStackLayout _requestRows = new() { Spacing = 8 };
    private readonly SkUiSheet _requestSheet;
    private readonly SkUiChipGroup _leaveType;
    private readonly SkUiDateRangePicker _leaveDays;
    private readonly SkUiLabel _requestError = new() { FontSize = 13, TextColor = Color.FromArgb("#B45309"), IsVisible = false };

    public ShiftsAndLeaveSample() : base(Info, fillsPage: true)
    {
        AddExamples();

        _list = new SkUiCollectionView
        {
            IsGrouped = true,
            IsStickyGroupHeader = true,
            ItemTemplate = new DataTemplate(() => new ShiftRow(this)),
            GroupHeaderTemplate = new DataTemplate(() =>
            {
                var label = new SkUiLabel
                {
                    Padding = new Thickness(16, 8),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = SampleColors.Caption,
                    TextTransform = TextTransform.Uppercase,
                    BackgroundColor = SampleColors.Page
                };
                label.SetBinding(SkUiLabel.TextProperty, static (ShiftWeek week) => week.Name);
                return label;
            }),
            EmptyView = new SkUiLabel { Text = "No shifts match the filters.", FontSize = 15, TextColor = SampleColors.Caption, Padding = new Thickness(16, 24), HorizontalTextAlignment = TextAlignment.Center }
        };
        var filters = new SkUiChipGroup
        {
            ItemsSource = new[] { Morning, Late, Night, Weekend },
            SelectedItems = _filters,
            Wraps = false,
            Padding = new Thickness(16, 0)
        };
        filters.SelectionChanged += (_, _) =>
        {
            // The rows under an open swipe view are about to change: close them first.
            _list.CloseSwipeViews();
            ShowMyShifts();
        };
        Grid.SetRow(_summary, 1);
        Grid.SetRow(_list, 2);
        _myShiftsTab.Content = new SkUiGrid
        {
            Padding = new Thickness(0, 12, 0, 0),
            RowSpacing = 8,
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children = { filters, _summary, _list }
        };
        _openShiftsTab.Content = new SkUiScrollView { Content = _open };
        _leaveTab.Content = new SkUiScrollView { Content = LeavePane() };

        _tabs.HeaderTemplate = HeaderTemplate();
        _tabs.AddTab(_myShiftsTab);
        _tabs.AddTab(_openShiftsTab);
        _tabs.AddTab(_leaveTab);

        _leaveType = new SkUiChipGroup
        {
            ItemsSource = new[] { Vacation, Sick, Personal },
            SelectionMode = SkUiChipSelectionMode.Single,
            SelectedItem = Vacation
        };
        _leaveDays = new SkUiDateRangePicker
        {
            Title = "Days",
            Placeholder = "Choose the first and the last day",
            DisplayFormat = "{0:ddd d MMM} – {1:ddd d MMM}",
            MinimumDate = DateTime.Today,
            UpdateMode = SkUiPickerUpdateMode.WhenFinished,
            TextColor = SampleColors.Ink,
            // The calendar opens in a sheet over the form's sheet, not in a popup of its own.
            PopupPresenter = new SkUiSheetPopupPresenter()
        };
        var send = new SkUiButton { Text = "Send request", FillColor = SampleColors.Accent, TextColor = Colors.White, CornerRadius = 8, PressedScale = 0.95, HorizontalOptions = LayoutOptions.End };
        send.Clicked += (_, _) => SendRequest();
        _requestSheet = new SkUiSheet
        {
            Title = "Request leave",
            Description = "Your manager is told at once; requested days count against your balance until they decide.",
            Detents = [SkUiSheetDetent.FitContent],
            Content = new SkUiVerticalStackLayout
            {
                Spacing = 10,
                Padding = new Thickness(16, 0, 16, 8),
                Children = { _leaveType, _leaveDays, new SkUiEntry { Placeholder = "Note for your manager (optional)", TextColor = SampleColors.Ink }, _requestError }
            },
            Actions = send
        };

        // The sheet is placed last in the grid that fills the page: closed, it draws nothing and lets pointers through.
        SampleContent = new SkUiGrid { Children = { _tabs, _requestSheet } };
        ShowMyShifts();
        ShowOpenShifts();
        ShowLeave();
    }

    /// <summary>
    /// Every tab's header: its title and a count pill, bound to the tab item. The pill is filled while the tab is selected
    /// (<see cref="SkUiTabItem.IsSelected"/>) and hidden without a count.
    /// </summary>
    private static DataTemplate HeaderTemplate() => new(() =>
    {
        var title = new SkUiLabel { FontSize = 15, VerticalOptions = LayoutOptions.Center };
        title.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.Header);
        title.SetBinding(SkUiLabel.TextColorProperty, static (SkUiTabItem tab) => tab.CurrentTextColor);
        title.SetBinding(SkUiLabel.FontAttributesProperty, static (SkUiTabItem tab) => tab.IsSelected,
            converter: new FuncConverter<bool>(selected => selected ? FontAttributes.Bold : FontAttributes.None));
        var count = new SkUiLabel { FontSize = 11, FontAttributes = FontAttributes.Bold, FontFeatures = Tabular, HorizontalTextAlignment = TextAlignment.Center };
        count.SetBinding(SkUiLabel.TextProperty, static (SkUiTabItem tab) => tab.BadgeText);
        count.SetBinding(SkUiLabel.TextColorProperty, static (SkUiTabItem tab) => tab.IsSelected,
            converter: new FuncConverter<bool>(selected => selected ? Colors.White : SampleColors.Caption));
        var pill = new SkUiBorder
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            Padding = new Thickness(6, 1),
            MinimumWidthRequest = 18,
            VerticalOptions = LayoutOptions.Center,
            Content = count
        };
        pill.SetBinding(BackgroundColorProperty, static (SkUiTabItem tab) => tab.IsSelected,
            converter: new FuncConverter<bool>(selected => selected ? SampleColors.Accent : TrackColor));
        pill.SetBinding(IsVisibleProperty, static (SkUiTabItem tab) => tab.BadgeText,
            converter: new FuncConverter<string?>(text => !string.IsNullOrEmpty(text)));
        return new SkUiHorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(12, 10),
            HorizontalOptions = LayoutOptions.Center,
            Children = { title, pill }
        };
    });

    // My shifts

    /// <summary>The list: the user's shifts that match the filters (none chosen: all), grouped by week.</summary>
    private void ShowMyShifts()
    {
        var shown = _myShifts
            .Where(shift => _filters.Count == 0 || _filters.Contains(shift.Kind) || (_filters.Contains(Weekend) && shift.IsWeekend))
            .OrderBy(shift => shift.Start)
            .ToList();
        var monday = MondayOf(DateTime.Today);
        _list.ItemsSource = shown
            .GroupBy(shift => (MondayOf(shift.Start) - monday).Days / 7)
            .Select(week => new ShiftWeek(week.Key switch { 0 => "This week", 1 => "Next week", _ => $"Week of {monday.AddDays(7 * week.Key):d MMM}" }, week))
            .ToList();
        var hours = shown.Aggregate(TimeSpan.Zero, (sum, shift) => sum + shift.Length);
        _summary.Text = $"{shown.Count} shifts · {(int)hours.TotalHours} h" + (_filters.Count > 0 ? " (filtered)" : string.Empty);
        _myShiftsTab.BadgeText = _myShifts.Count.ToString(CultureInfo.CurrentCulture);
    }

    private void RequestSwap(Shift shift)
    {
        shift.SwapRequested = true;
        ShowMyShifts();
        SkUiAccessibility.Announce(_summary, $"Swap requested for {shift.Start:dddd} {shift.Times}.");
    }

    private async Task DropAsync(SkUiView anchor, Shift shift)
    {
        var keep = new SkUiButton { Text = "Keep it", TextColor = SampleColors.Accent, FillColor = Colors.Transparent };
        var drop = new SkUiButton { Text = "Drop", FillColor = DropColor, TextColor = Colors.White, CornerRadius = 8 };
        var message = new SkUiLabel
        {
            Text = $"{shift.Start:dddd d MMMM}, {shift.Times}. It goes back to the open shifts for your team.",
            FontSize = 15,
            TextColor = SampleColors.Ink,
            Margin = new Thickness(16, 0, 16, 8)
        };
        var dropped = await SkUiSheet.ShowAsync<bool>(anchor, message, sheet =>
        {
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            sheet.Title = "Drop this shift?";
            sheet.Actions = new SkUiHorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { keep, drop } };
            keep.Clicked += (_, _) => sheet.Close(false);
            drop.Clicked += (_, _) => sheet.Close(true);
        });
        if (!dropped)
            return;
        _myShifts.Remove(shift);
        shift.SwapRequested = false;
        _openShifts.Add(shift);
        ShowMyShifts();
        ShowOpenShifts();
    }

    /// <summary>
    /// A row of My shifts: the day, the times and the role, with "Swap" and "Drop" revealed by a swipe to the left. Every row is in
    /// the same exclusive group, so one stays open at a time.
    /// </summary>
    private sealed class ShiftRow : SkUiSwipeView
    {
        private readonly SkUiLabel _weekday = new() { FontSize = 12, TextColor = SampleColors.Caption, HorizontalTextAlignment = TextAlignment.Center };
        private readonly SkUiLabel _day = new() { FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, HorizontalTextAlignment = TextAlignment.Center };
        private readonly SkUiLabel _times = new() { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, FontFeatures = Tabular };
        private readonly SkUiLabel _what = new() { FontSize = 13, TextColor = SampleColors.Caption };
        private readonly SkUiLabel _swap = new() { Text = "Swap requested", FontSize = 12, TextColor = SwapColor, VerticalOptions = LayoutOptions.Center };

        public ShiftRow(ShiftsAndLeaveSample page)
        {
            ExclusiveGroup = ShiftRowsGroup;
            var swap = new SwipeItem { Text = "Swap", BackgroundColor = SwapColor };
            swap.Invoked += (_, _) =>
            {
                if (BindingContext is Shift shift)
                    page.RequestSwap(shift);
            };
            var drop = new SwipeItem { Text = "Drop", BackgroundColor = DropColor };
            drop.Invoked += async (_, _) =>
            {
                if (BindingContext is Shift shift)
                    await page.DropAsync(this, shift);
            };
            // The first item sits at the trailing edge: the destructive one goes there, as on iOS and Android.
            RightItems = new SwipeItems([drop, swap]) { Mode = SwipeMode.Reveal };

            var date = new SkUiVerticalStackLayout { WidthRequest = 44, VerticalOptions = LayoutOptions.Center, Children = { _weekday, _day } };
            var text = new SkUiVerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _times, _what } };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(_swap, 2);
            var separator = new SkUiBox { Color = SampleColors.Border, HeightRequest = 1, VerticalOptions = LayoutOptions.End };
            Grid.SetColumnSpan(separator, 3);
            // Opaque, so the row slides over the list and its items instead of showing them through.
            Content = new SkUiGrid
            {
                BackgroundColor = SampleColors.Surface,
                Padding = new Thickness(16, 10, 16, 0),
                ColumnSpacing = 14,
                ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(10)],
                Children = { date, text, _swap, Row(separator, 1) }
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not Shift shift)
                return;
            _weekday.Text = shift.Start.ToString("ddd", CultureInfo.CurrentCulture);
            _day.Text = shift.Start.Day.ToString(CultureInfo.CurrentCulture);
            _times.Text = shift.Times;
            _what.Text = $"{shift.Role} · {shift.Place}";
            _swap.IsVisible = shift.SwapRequested;
        }

        private static SkUiView Row(SkUiView view, int row)
        {
            Grid.SetRow(view, row);
            return view;
        }
    }

    // Open shifts

    private void ShowOpenShifts()
    {
        _open.Children.Clear();
        _open.Children.Add(new SkUiLabel { Text = "Shifts nobody has yet. Claim one to add it to your shifts.", FontSize = 14, TextColor = SampleColors.Caption });
        foreach (var shift in _openShifts.OrderBy(shift => shift.Start))
        {
            var claim = new SkUiChip
            {
                Text = "Claim",
                Kind = SkUiChipKind.Action,
                TextColor = SampleColors.Accent,
                StrokeColor = SampleColors.Accent,
                PressedScale = 0.92,
                VerticalOptions = LayoutOptions.Center
            };
            claim.Clicked += (_, _) => Claim(shift);
            var text = new SkUiVerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new SkUiLabel { Text = shift.Start.ToString("dddd d MMMM", CultureInfo.CurrentCulture), FontSize = 13, TextColor = SampleColors.Caption },
                    new SkUiLabel { Text = shift.Times, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, FontFeatures = Tabular },
                    new SkUiLabel { Text = $"{shift.Role} · {shift.Place}", FontSize = 13, TextColor = SampleColors.Caption }
                }
            };
            Grid.SetColumn(claim, 1);
            _open.Children.Add(Card(new SkUiGrid
            {
                ColumnSpacing = 12,
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Children = { text, claim }
            }));
        }
        if (_openShifts.Count == 0)
            _open.Children.Add(new SkUiLabel { Text = "No open shifts right now.", FontSize = 15, TextColor = SampleColors.Ink });
        _openShiftsTab.BadgeText = _openShifts.Count > 0 ? _openShifts.Count.ToString(CultureInfo.CurrentCulture) : null;
    }

    private void Claim(Shift shift)
    {
        _openShifts.Remove(shift);
        _myShifts.Add(shift);
        ShowOpenShifts();
        ShowMyShifts();
        // The claimed card is gone, so nothing on screen says it: say it once.
        SkUiAccessibility.Announce(_open, $"Claimed {shift.Start:dddd} {shift.Times}. It is in My shifts.");
    }

    // Leave

    private SkUiVerticalStackLayout LeavePane()
    {
        var rings = new SkUiGrid { ColumnSpacing = 8, ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)] };
        for (var index = 0; index < _balances.Length; index++)
        {
            var balance = _balances[index];
            _ringValues[index] = new SkUiLabel { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, FontFeatures = Tabular, HorizontalTextAlignment = TextAlignment.Center };
            _rings[index] = new SkUiProgressRing
            {
                WidthRequest = 96,
                HeightRequest = 96,
                Thickness = 8,
                ProgressColor = balance.Color,
                TrackColor = TrackColor,
                HorizontalOptions = LayoutOptions.Center,
                Content = new SkUiVerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Children = { _ringValues[index], new SkUiLabel { Text = "days left", FontSize = 11, TextColor = SampleColors.Caption, HorizontalTextAlignment = TextAlignment.Center } }
                }
            };
            SemanticProperties.SetDescription(_rings[index], $"{balance.Type} left");
            var column = new SkUiVerticalStackLayout
            {
                Spacing = 6,
                Children = { _rings[index], new SkUiLabel { Text = balance.Type, FontSize = 13, TextColor = SampleColors.Ink, HorizontalTextAlignment = TextAlignment.Center } }
            };
            Grid.SetColumn(column, index);
            rings.Children.Add(column);
        }

        var vacation = new SkUiStackedBar { HeightRequest = 14, TrackColor = TrackColor, Segments = { _vacationTaken, _vacationRequested, _vacationLeft } };
        SemanticProperties.SetDescription(vacation, "Vacation days this year");
        var request = new SkUiButton
        {
            Text = "Request leave",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            FillColor = SampleColors.Accent,
            TextColor = Colors.White,
            CornerRadius = 12,
            Padding = new Thickness(12, 14),
            PressedScale = 0.95
        };
        request.Clicked += (_, _) =>
        {
            _requestError.IsVisible = false;
            _requestSheet.IsOpen = true;
        };
        return new SkUiVerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(16),
            Children =
            {
                Card(Heading("Balances"), rings),
                Card(Heading($"Vacation {DateTime.Today.Year}"), vacation, _vacationLegend, Legend()),
                request,
                Card(Heading("Requests"), _requestRows)
            }
        };
    }

    private void SendRequest()
    {
        if (_leaveDays.Value is not { IsComplete: true } range)
        {
            _requestError.Text = "Choose the first and the last day.";
            _requestError.IsVisible = true;
            return;
        }
        var type = _leaveType.SelectedItem as string ?? Vacation;
        var days = Enumerable.Range(0, range.Days).Count(offset => range.Start.AddDays(offset).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        var balance = _balances.First(entry => entry.Type == type);
        if (days == 0 || days > balance.Left)
        {
            _requestError.Text = days == 0 ? "Those are weekend days only." : $"You have {balance.Left} {type.ToLowerInvariant()} days left.";
            _requestError.IsVisible = true;
            return;
        }
        balance.Requested += days;
        _requests.Insert(0, new LeaveRequest(range, type, days, Pending));
        _leaveDays.Value = null;
        _requestSheet.IsOpen = false;
        ShowLeave();
        SkUiAccessibility.Announce(_requestRows, $"{type} request for {days} days sent.");
    }

    /// <summary>The balances: rings of the days left, the vacation bar (tweens to the new parts), the requests.</summary>
    private void ShowLeave()
    {
        for (var index = 0; index < _balances.Length; index++)
        {
            var balance = _balances[index];
            _rings[index].Progress = (double)balance.Left / balance.Allowance;
            _ringValues[index].Text = balance.Left.ToString(CultureInfo.CurrentCulture);
        }
        var vacation = _balances[0];
        _vacationTaken.Value = vacation.Taken;
        _vacationRequested.Value = vacation.Requested;
        _vacationLeft.Value = vacation.Left;
        _vacationLegend.Text = $"{vacation.Taken} taken · {vacation.Requested} requested · {vacation.Left} available of {vacation.Allowance} days";

        _requestRows.Children.Clear();
        foreach (var request in _requests)
        {
            var status = new SkUiLabel
            {
                Text = request.Status,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = request.Status == Approved ? VacationColor : RequestedColor,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(status, 1);
            var when = request.Range.Days == 1 ? $"{request.Range.Start:ddd d MMM}" : $"{request.Range.Start:d MMM} – {request.Range.LastDay:d MMM}";
            _requestRows.Children.Add(new SkUiGrid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
                Children =
                {
                    new SkUiVerticalStackLayout
                    {
                        Children =
                        {
                            new SkUiLabel { Text = $"{request.Type} · {request.Days} {(request.Days == 1 ? "day" : "days")}", FontSize = 15, TextColor = SampleColors.Ink },
                            new SkUiLabel { Text = when, FontSize = 13, TextColor = SampleColors.Caption }
                        }
                    },
                    status
                }
            });
        }
        if (_requests.Count == 0)
            _requestRows.Children.Add(new SkUiLabel { Text = "No requests yet.", FontSize = 14, TextColor = SampleColors.Caption });
        _leaveTab.BadgeText = _requests.Count(request => request.Status == Pending) is var pending and > 0 ? pending.ToString(CultureInfo.CurrentCulture) : null;
    }

    private static SkUiWrapLayout Legend()
    {
        var legend = new SkUiWrapLayout { Spacing = 14, RowSpacing = 6 };
        foreach (var (color, text) in new[] { (TakenColor, "Taken"), (RequestedColor, "Requested"), (VacationColor, "Available") })
            legend.Children.Add(new SkUiHorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new SkUiBox { Color = color, CornerRadius = 4, WidthRequest = 8, HeightRequest = 8, VerticalOptions = LayoutOptions.Center },
                    new SkUiLabel { Text = text, FontSize = 12, TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center }
                }
            });
        return legend;
    }

    // Data

    /// <summary>Shifts around today (the user's and open ones) and an approved vacation, so the page has something to show.</summary>
    private void AddExamples()
    {
        var today = DateTime.Today;
        (int Day, int Hour, int Hours, string Role, string Place)[] mine =
        [
            (0, 7, 8, "Barista", "Harbour Street"), (1, 14, 8, "Barista", "Harbour Street"), (2, 7, 8, "Shift lead", "Station Square"),
            (4, 22, 8, "Baker", "Central kitchen"), (5, 9, 6, "Barista", "Harbour Street"), (7, 7, 8, "Shift lead", "Station Square"),
            (8, 14, 8, "Barista", "Harbour Street"), (10, 22, 8, "Baker", "Central kitchen"), (12, 9, 6, "Barista", "Market Hall")
        ];
        foreach (var (day, hour, hours, role, place) in mine)
            _myShifts.Add(new Shift(today.AddDays(day).AddHours(hour), TimeSpan.FromHours(hours), role, place));
        (int Day, int Hour, int Hours, string Role, string Place)[] open =
        [
            (3, 7, 8, "Barista", "Market Hall"), (6, 10, 6, "Barista", "Harbour Street"), (9, 22, 8, "Baker", "Central kitchen")
        ];
        foreach (var (day, hour, hours, role, place) in open)
            _openShifts.Add(new Shift(today.AddDays(day).AddHours(hour), TimeSpan.FromHours(hours), role, place));
        var vacation = new SkUiDateRange(today.AddDays(-60), today.AddDays(-56));
        _requests.Add(new LeaveRequest(vacation, Vacation, 5, Approved));
    }

    private static DateTime MondayOf(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));

    private static SkUiBorder Card(params SkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 12 };
        foreach (var child in children)
            stack.Children.Add(child);
        return new SkUiBorder
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(16),
            Content = stack
        };
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };

    /// <summary>A one-way converter from a function, for the header template's bindings.</summary>
    private sealed class FuncConverter<T>(Func<T, object?> convert) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is T typed ? convert(typed) : convert(default!);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
