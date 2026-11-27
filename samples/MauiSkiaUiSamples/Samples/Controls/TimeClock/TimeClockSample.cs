using System.Globalization;
using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls.TimeClock;

/// <summary>
/// HOWTO: the time clock of an employee self-service app, built from the 1.18 controls:
/// <list type="bullet">
/// <item>An <see cref="SkUiProgressRing"/> shows the hours worked against the day's target, with the running time inside it. The time
/// ticks every second in tabular figures (<see cref="SkUiLabel.FontFeatures"/> <c>[SkUiFontFeature.TabularFigures]</c>), so its digits
/// keep their places; the "Tabular figures" switch turns them proportional (<c>pnum</c>) to show the jitter it prevents.</item>
/// <item>The status line is a polite live region (<see cref="SkUiAccessibility.LiveRegionProperty"/>): a screen reader reads
/// "On a coffee break since 13:50" when it changes, without the user going to it. The ticking time is not a live region (it
/// would talk every second); the ring is marked <c>UpdatesFrequently</c> instead. When a break reaches its planned length,
/// <see cref="SkUiAccessibility.Announce(SkUiView, string, SkUiLiveRegion)"/> says so once.</item>
/// <item>"Take a break" opens a sheet fitted to its content with <see cref="SkUiSheet.ShowAsync{TResult}"/>: a chip group picks
/// the length, a row picks the type and closes the sheet with both. "Clock out" asks in a dialog (the same call with
/// <c>Presentation = Dialog</c>), which completes with the button's <c>true</c> or <c>false</c>, or <c>default</c> (<c>false</c>) when dismissed.</item>
/// <item><see cref="SkUiStackedBar"/>s show the day (worked, breaks, still to work) and the week (regular hours, overtime per day).
/// Each change tweens the parts with the look's transition.</item>
/// <item>The big buttons shrink a little while pressed (<see cref="SkUiView.PressedScale"/>); the small "?" is drawn 26 DIPs
/// wide but pressed in 44 × 44 (<see cref="SkUiView.MinimumTouchTargetSize"/>).</item>
/// </list>
/// The clock is simulated: the page starts in the afternoon of a day that began at 8:02, and "Fast-forward" runs a minute per second.
/// </summary>
public sealed class TimeClockSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Time clock (ring, live status, break sheet)",
        Summary: "The clock-in screen of an employee app: a progress ring with the running time in tabular figures, a status a screen " +
                 "reader reads when it changes, a break picked in a bottom sheet, clock-out confirmed in a dialog, and stacked bars of the " +
                 "day and the week.",
        HowTo:
        [
            "Put content in an `SkUiProgressRing` (it is a content view): the content is laid out in the square inside the ring. " +
                "Set `Progress` (0–1), `ProgressColor`, `TrackColor` and `Thickness`.",
            "Make a ticking time keep still with `FontFeatures = [SkUiFontFeature.TabularFigures]` (XAML: `FontFeatures=\"tnum\"`): every " +
                "digit gets the same width.",
            "Mark a status line `SkUiAccessibility.SetLiveRegion(label, SkUiLiveRegion.Polite)` so screen readers read its new text; " +
                "say one-off things with `SkUiAccessibility.Announce(anchor, text)`.",
            "Ask for a choice with `await SkUiSheet.ShowAsync<TResult>(anchor, content, sheet => …)`: set `Title`, `Detents = " +
                "[SkUiSheetDetent.FitContent]` or `Presentation = SkUiSheetPresentation.Dialog`, and call `sheet.Close(result)` from a button.",
            "Show parts of a whole with `SkUiStackedBar`: one `SkUiBarSegment` (`Value`, `Color`, `Label`) per part; `Maximum` is what " +
                "the whole bar stands for (the rest shows `TrackColor`).",
            "Give a big button a press scale (`PressedScale = 0.95`) and a small one a touch target (`MinimumTouchTargetSize = 44`)."
        ],
        ThingsToKnow:
        [
            "Many fonts already draw tabular digits (Open Sans, Roboto); San Francisco and Segoe UI draw proportional ones, so a timer " +
                "without `tnum` jitters there. The switch sets `pnum` to show the difference everywhere.",
            "Never make text that changes every second a live region: screen readers would read it every second. Mark the value " +
                "`UpdatesFrequently` instead, as the ring here.",
            "`ShowAsync` returns `default` when the user dismisses the sheet (a scrim tap, a drag down, Back, Escape): use a reference " +
                "or nullable result type to tell that apart from a choice.",
            "A stacked bar tweens every change (250 ms with the default look); under reduce motion it jumps.",
            "The modal sheet takes the page out of what screen readers read, so the status is not read while the break sheet is open."
        ]);

    private const string Lunch = "Lunch";
    private const string Coffee = "Coffee";
    private const string Personal = "Personal";

    private static readonly TimeSpan Target = TimeSpan.FromHours(8);
    private static readonly TimeSpan WeekTarget = TimeSpan.FromHours(40);
    private static readonly Color WorkColor = SampleColors.Accent;
    private static readonly Color BreakColor = Color.FromArgb("#F59E0B");
    private static readonly Color OvertimeColor = Color.FromArgb("#7C3AED");
    private static readonly Color TrackColor = Color.FromArgb("#E6ECEF");
    private static readonly Color DangerColor = Color.FromArgb("#C62828");
    private static readonly SkUiFontFeatures Tabular = [SkUiFontFeature.TabularFigures];
    private static readonly SkUiFontFeatures Proportional = [SkUiFontFeature.ProportionalFigures];

    private enum ClockState { Out, Working, OnBreak }

    /// <summary>A break the user picked in the sheet: its type and planned length.</summary>
    private sealed record BreakPlan(string Type, int Minutes);

    /// <summary>A length chip: the group shows an item's <c>ToString()</c> when it has no <c>ItemDisplayBinding</c>.</summary>
    private sealed record BreakLength(int Minutes)
    {
        public override string ToString() => $"{Minutes} min";
    }

    // The simulated day.
    private DateTime _now = DateTime.Today.Add(new TimeSpan(13, 46, 5));
    private ClockState _state = ClockState.Working;
    private DateTime _clockedInAt = DateTime.Today.Add(new TimeSpan(8, 2, 0));
    private DateTime _since = DateTime.Today.Add(new TimeSpan(8, 2, 0)); // when the current state began (a break's start)
    private TimeSpan _worked = new(5, 14, 5);
    private TimeSpan _breaks = TimeSpan.FromMinutes(30);
    private BreakPlan? _break;
    private bool _breakOverAnnounced;
    private bool _fastForward;
    private IDispatcherTimer? _timer;

    private readonly SkUiLabel _status = new() { FontSize = 15, TextColor = SampleColors.Caption };
    private readonly SkUiLabel _time = new() { FontSize = 30, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, HorizontalTextAlignment = TextAlignment.Center, MaxLines = 1 };
    private readonly SkUiLabel _ringCaption = new() { FontSize = 13, TextColor = SampleColors.Caption, HorizontalTextAlignment = TextAlignment.Center };
    private readonly SkUiProgressRing _ring;
    private readonly SkUiButton _clock;
    private readonly SkUiButton _takeBreak;
    private readonly SkUiBarSegment _dayWorked = new(0, WorkColor, "Worked");
    private readonly SkUiBarSegment _dayBreaks = new(0, BreakColor, "Breaks");
    private readonly SkUiStackedBar _day;
    private readonly SkUiLabel _dayLegend = new() { FontSize = 13, TextColor = SampleColors.Caption, FontFeatures = Tabular };
    private readonly SkUiGrid _week = new() { ColumnSpacing = 10, RowSpacing = 8 };
    private readonly SkUiLabel _weekTotal = new() { FontSize = 13, TextColor = SampleColors.Caption, FontFeatures = Tabular };
    private readonly TimeSpan[] _weekHours = new TimeSpan[7];
    private readonly (SkUiBarSegment Regular, SkUiBarSegment Overtime, SkUiLabel? Hours)[] _weekRows = new (SkUiBarSegment, SkUiBarSegment, SkUiLabel?)[7];
    private readonly int _today = ((int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

    public TimeClockSample() : base(Info)
    {
        // The status is read by screen readers whenever its text changes (on clock in, out and breaks: not every second).
        SkUiAccessibility.SetLiveRegion(_status, SkUiLiveRegion.Polite);
        _time.FontFeatures = Tabular;

        _ring = new SkUiProgressRing
        {
            WidthRequest = 240,
            HeightRequest = 240,
            Thickness = 14,
            TrackColor = TrackColor,
            HorizontalOptions = LayoutOptions.Center,
            Content = new SkUiVerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { _time, _ringCaption } }
        };
        SemanticProperties.SetDescription(_ring, "Hours worked today");
        SkUiAccessibility.SetUpdatesFrequently(_ring, true);

        _clock = BigButton(string.Empty, WorkColor, Colors.White);
        _clock.Clicked += async (_, _) => await ClockInOrOutAsync();
        _takeBreak = BigButton(string.Empty, SampleColors.Surface, SampleColors.Ink);
        _takeBreak.BorderColor = SampleColors.Border;
        _takeBreak.BorderWidth = 1;
        _takeBreak.Clicked += async (_, _) => await StartOrEndBreakAsync();
        Grid.SetColumn(_takeBreak, 1);

        var tabular = new SkUiSwitch { IsToggled = true, OnColor = WorkColor, VerticalOptions = LayoutOptions.Center };
        tabular.Toggled += (_, args) => _time.FontFeatures = args.Value ? Tabular : Proportional;
        var fast = new SkUiSwitch { OnColor = WorkColor, VerticalOptions = LayoutOptions.Center };
        fast.Toggled += (_, args) => _fastForward = args.Value;

        _day = new SkUiStackedBar { HeightRequest = 14, TrackColor = TrackColor, Segments = { _dayWorked, _dayBreaks } };
        SemanticProperties.SetDescription(_day, "Today");
        AddWeekExamples();
        BuildWeek();

        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                Card(
                    Header(),
                    _ring,
                    new SkUiGrid
                    {
                        ColumnSpacing = 10,
                        ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
                        Children = { _clock, _takeBreak }
                    },
                    new SkUiWrapLayout
                    {
                        Spacing = 16,
                        RowSpacing = 4,
                        Children = { Labeled("Tabular figures", tabular), Labeled("Fast-forward", fast) }
                    }),
                Card(Heading("Today"), _day, _dayLegend),
                Card(Heading("This week"), _week, _weekTotal)
            }
        };
        Show();
    }

    /// <summary>The day, the live status, and a small "?" with a 44 × 44 touch target.</summary>
    private SkUiGrid Header()
    {
        var day = new SkUiLabel { Text = _now.ToString("dddd d MMMM", CultureInfo.CurrentCulture), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink };
        var help = new SkUiButton
        {
            Text = "?",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = SampleColors.Accent,
            FillColor = Colors.Transparent,
            BorderColor = SampleColors.Border,
            BorderWidth = 1,
            CornerRadius = 13,
            Padding = Thickness.Zero,
            WidthRequest = 26,
            HeightRequest = 26,
            // Drawn 26 DIPs wide, pressed anywhere in 44 × 44 around it; the layout does not change.
            MinimumTouchTargetSize = 44,
            VerticalOptions = LayoutOptions.Start
        };
        SemanticProperties.SetDescription(help, "How the time is counted");
        help.Clicked += async (_, _) => await ExplainAsync(help);
        Grid.SetColumn(help, 1);
        return new SkUiGrid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Children = { new SkUiVerticalStackLayout { Spacing = 2, Children = { day, _status } }, help }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_timer is not null || Dispatcher is null)
            return;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Tick(_fastForward ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1));
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // The Source page goes over this one; the clock stops while it is not shown.
        _timer?.Stop();
        _timer = null;
    }

    /// <summary>The simulated time moves on: the worked time or the break grows, and everything shown follows.</summary>
    internal void Tick(TimeSpan elapsed)
    {
        _now += elapsed;
        switch (_state)
        {
            case ClockState.Working:
                _worked += elapsed;
                break;
            case ClockState.OnBreak:
                _breaks += elapsed;
                if (_break is { } plan && !_breakOverAnnounced && _now - _since >= TimeSpan.FromMinutes(plan.Minutes))
                {
                    _breakOverAnnounced = true;
                    // Once, anchored on the status without changing its text, so its live region says nothing more.
                    SkUiAccessibility.Announce(_status, $"Your {plan.Minutes}-minute {plan.Type.ToLowerInvariant()} break is over.", SkUiLiveRegion.Assertive);
                }
                break;
        }
        Show();
    }

    internal async Task ClockInOrOutAsync()
    {
        if (_state == ClockState.Out)
        {
            SetState(ClockState.Working);
            return;
        }
        // A dialog: no detents, no drag; it completes with the value passed to Close, or false when dismissed.
        var cancel = new SkUiButton { Text = "Keep working", TextColor = SampleColors.Accent, FillColor = Colors.Transparent };
        var clockOut = new SkUiButton { Text = "Clock out", FillColor = DangerColor, TextColor = Colors.White, CornerRadius = 8, PressedScale = 0.95 };
        var message = new SkUiLabel
        {
            Text = $"You worked {Hours(_worked)} today, with {Hours(_breaks)} of breaks.",
            FontSize = 15,
            TextColor = SampleColors.Ink,
            Margin = new Thickness(16, 0, 16, 8),
            FontFeatures = Tabular
        };
        var confirmed = await SkUiSheet.ShowAsync<bool>(_clock, message, sheet =>
        {
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            sheet.Title = "Clock out?";
            sheet.Actions = new SkUiHorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { cancel, clockOut } };
            cancel.Clicked += (_, _) => sheet.Close(false);
            clockOut.Clicked += (_, _) => sheet.Close(true);
        });
        if (confirmed)
            SetState(ClockState.Out);
    }

    internal async Task StartOrEndBreakAsync()
    {
        if (_state == ClockState.OnBreak)
        {
            SetState(ClockState.Working);
            return;
        }
        var plan = await PickBreakAsync();
        if (plan is null)
            return; // dismissed: a scrim tap, a drag down, Back or Escape
        _break = plan;
        _breakOverAnnounced = false;
        SetState(ClockState.OnBreak);
    }

    /// <summary>
    /// A bottom sheet as tall as its content: the length in a chip group (one selected), then a row per break type, which closes the
    /// sheet with the type and the length.
    /// </summary>
    private Task<BreakPlan?> PickBreakAsync()
    {
        BreakLength[] lengths = [new(10), new(15), new(30), new(45)];
        var length = new SkUiChipGroup
        {
            ItemsSource = lengths,
            SelectionMode = SkUiChipSelectionMode.Single,
            SelectedItem = lengths[1],
            Wraps = false,
            Padding = new Thickness(16, 0)
        };
        var rows = new SkUiVerticalStackLayout { Padding = new Thickness(0, 4, 0, 12) };
        var content = new SkUiVerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new SkUiLabel { Text = "Length", FontSize = 13, TextColor = SampleColors.Caption, Margin = new Thickness(16, 0) },
                length,
                rows
            }
        };
        return SkUiSheet.ShowAsync<BreakPlan>(_takeBreak, content, sheet =>
        {
            sheet.Title = "Take a break";
            sheet.Description = "Breaks do not count as work time. Your manager sees the type you choose.";
            sheet.Detents = [SkUiSheetDetent.FitContent];
            foreach (var (type, note) in new[] { (Lunch, "Unpaid, once a day"), (Coffee, "A short pause"), (Personal, "An errand, a call") })
            {
                var row = new SkUiButton
                {
                    Text = $"{type}  ·  {note}",
                    FontSize = 16,
                    TextColor = SampleColors.Ink,
                    FillColor = Colors.Transparent,
                    CornerRadius = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    HorizontalTextAlignment = TextAlignment.Start,
                    Padding = new Thickness(16, 14)
                };
                row.Clicked += (_, _) => sheet.Close(new BreakPlan(type, (length.SelectedItem as BreakLength ?? lengths[1]).Minutes));
                rows.Children.Add(row);
            }
        });
    }

    private Task ExplainAsync(SkUiView anchor)
    {
        var ok = new SkUiButton { Text = "OK", TextColor = SampleColors.Accent, FillColor = Colors.Transparent, HorizontalOptions = LayoutOptions.End };
        var text = new SkUiLabel
        {
            Text = $"Your day's target is {Hours(Target)} and the week's {Hours(WeekTarget)}. Breaks are not work time. " +
                   "Time past the day's target counts as overtime.",
            FontSize = 15,
            TextColor = SampleColors.Ink,
            Margin = new Thickness(16, 0, 16, 8)
        };
        return SkUiSheet.ShowAsync<bool>(anchor, text, sheet =>
        {
            sheet.Presentation = SkUiSheetPresentation.Dialog;
            sheet.Title = "How the time is counted";
            sheet.Actions = ok;
            ok.Clicked += (_, _) => sheet.Close(true);
        });
    }

    private void SetState(ClockState state)
    {
        if (state == ClockState.Working && _state == ClockState.Out)
            _clockedInAt = _now; // clocking in starts a new session; ending a break keeps this time
        _state = state;
        _since = _now;
        if (state != ClockState.OnBreak)
            _break = null;
        Show();
    }

    /// <summary>Everything shown from the state: the status (a live region), the ring, the buttons and the bars.</summary>
    private void Show()
    {
        var since = _since.ToString("t", CultureInfo.CurrentCulture);
        // Assigning the same text is no change: the live region reads only new text.
        _status.Text = _state switch
        {
            ClockState.Working => $"Clocked in at {_clockedInAt.ToString("t", CultureInfo.CurrentCulture)}",
            ClockState.OnBreak => $"On a {_break?.Type.ToLowerInvariant()} break since {since}",
            _ => $"Clocked out at {since}"
        };

        var overtime = _worked - Target;
        if (_state == ClockState.OnBreak && _break is { } plan)
        {
            var left = TimeSpan.FromMinutes(plan.Minutes) - (_now - _since);
            _time.Text = Clock(_now - _since);
            _ringCaption.Text = left > TimeSpan.Zero ? $"break · {Hours(left)} left" : $"break · over by {Hours(-left)}";
        }
        else
        {
            _time.Text = Clock(_worked);
            _ringCaption.Text = overtime > TimeSpan.Zero ? $"+{Hours(overtime)} overtime" : $"worked of {Hours(Target)}";
        }
        // On a break the ring fills with the break's planned length; otherwise with the day's target.
        _ring.Progress = _state == ClockState.OnBreak && _break is { } current
            ? Math.Min(1, (_now - _since) / TimeSpan.FromMinutes(current.Minutes))
            : Math.Min(1, _worked / Target);
        _ring.ProgressColor = _state switch
        {
            ClockState.OnBreak => BreakColor,
            ClockState.Out => SampleColors.Caption,
            _ => overtime > TimeSpan.Zero ? OvertimeColor : WorkColor
        };

        _clock.Text = _state == ClockState.Out ? "Clock in" : "Clock out";
        _clock.FillColor = _state == ClockState.Out ? WorkColor : DangerColor;
        _takeBreak.Text = _state == ClockState.OnBreak ? "End break" : "Take a break";
        _takeBreak.IsEnabled = _state != ClockState.Out;

        // The day: worked and breaks, out of the target plus the breaks taken, so the track is what is still to work.
        _dayWorked.Value = _worked.TotalMinutes;
        _dayBreaks.Value = _breaks.TotalMinutes;
        _day.Maximum = Math.Max(Target.TotalMinutes, _worked.TotalMinutes) + _breaks.TotalMinutes;
        var toWork = Target - _worked;
        _dayLegend.Text = $"Worked {Hours(_worked)}  ·  Breaks {Hours(_breaks)}  ·  " +
                          (toWork > TimeSpan.Zero ? $"Left {Hours(toWork)}" : $"Overtime {Hours(-toWork)}");

        _weekHours[_today] = _worked;
        ShowWeekRow(_today);
        var total = _weekHours.Aggregate(TimeSpan.Zero, (sum, hours) => sum + hours);
        _weekTotal.Text = $"{Hours(total)} of {Hours(WeekTarget)} this week";
    }

    /// <summary>One row per day: the day's name, a bar of regular hours and overtime out of 10 h, the hours.</summary>
    private void BuildWeek()
    {
        _week.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
        var monday = DateTime.Today.AddDays(-_today);
        for (var day = 0; day < 7; day++)
        {
            _week.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var isToday = day == _today;
            var name = new SkUiLabel
            {
                Text = monday.AddDays(day).ToString("ddd", CultureInfo.CurrentCulture),
                FontSize = 14,
                WidthRequest = 40,
                FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isToday ? SampleColors.Accent : SampleColors.Ink,
                VerticalOptions = LayoutOptions.Center
            };
            var regular = new SkUiBarSegment(0, WorkColor, "Regular");
            var overtime = new SkUiBarSegment(0, OvertimeColor, "Overtime");
            var bar = new SkUiStackedBar { HeightRequest = 10, Maximum = 10 * 60, TrackColor = TrackColor, VerticalOptions = LayoutOptions.Center, Segments = { regular, overtime } };
            SemanticProperties.SetDescription(bar, monday.AddDays(day).ToString("dddd", CultureInfo.CurrentCulture));
            var hours = new SkUiLabel { FontSize = 14, TextColor = SampleColors.Ink, FontFeatures = Tabular, WidthRequest = 48, HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center };
            Place(name, 0, day);
            Place(bar, 1, day);
            Place(hours, 2, day);
            _week.Children.Add(name);
            _week.Children.Add(bar);
            _week.Children.Add(hours);
            _weekRows[day] = (regular, overtime, hours);
            ShowWeekRow(day);
        }
    }

    private void ShowWeekRow(int day)
    {
        var (regular, overtime, hours) = _weekRows[day];
        if (hours is null)
            return; // the week is not built yet
        var worked = _weekHours[day];
        regular.Value = Math.Min(worked.TotalMinutes, Target.TotalMinutes);
        overtime.Value = Math.Max(0, (worked - Target).TotalMinutes);
        hours.Text = day > _today ? "–" : Clock(worked, seconds: false);
    }

    /// <summary>Hours worked on the days before today (none at the weekend).</summary>
    private void AddWeekExamples()
    {
        TimeSpan[] past = [new(8, 12, 0), new(7, 48, 0), new(9, 31, 0), new(8, 0, 0), new(6, 25, 0)];
        for (var day = 0; day < _today && day < past.Length; day++)
            _weekHours[day] = past[day];
    }

    private static string Clock(TimeSpan time, bool seconds = true) => seconds
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
        : $"{(int)time.TotalHours}:{time.Minutes:00}";

    private static string Hours(TimeSpan time) => time.TotalHours >= 1
        ? time.Minutes == 0 ? $"{(int)time.TotalHours} h" : $"{(int)time.TotalHours} h {time.Minutes} min"
        : $"{Math.Max(0, (int)Math.Ceiling(time.TotalMinutes))} min";

    private static void Place(SkUiView view, int column, int row)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
    }

    private static SkUiButton BigButton(string text, Color fill, Color textColor) => new()
    {
        Text = text,
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        FillColor = fill,
        TextColor = textColor,
        CornerRadius = 12,
        Padding = new Thickness(12, 14),
        // Shrinks to 95 % about its center while pressed: composited, nothing is drawn again.
        PressedScale = 0.95
    };

    private static SkUiHorizontalStackLayout Labeled(string text, SkUiView control) => new()
    {
        Spacing = 8,
        Children = { new SkUiLabel { Text = text, FontSize = 14, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center }, control }
    };

    private static SkUiBorder Card(params SkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 14 };
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
}
