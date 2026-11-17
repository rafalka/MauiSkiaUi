using System.Globalization;
using MauiSkiaUi;
using Microsoft.Maui.Controls.Shapes;

namespace MauiSkiaUiSamples.Samples.Controls.Calendar;

/// <summary>
/// HOWTO: a month calendar over the app's own data. A small team calendar:
/// <list type="bullet">
/// <item>Appointments are dots under the day, one per appointment in its category's color (at most three).</item>
/// <item>Out-of-office days are an amber bar on a tinted day; they are added with a date range picker (<c>SkUiDateRangePicker</c>)
/// that waits for OK.</item>
/// <item>Public holidays (made up for this page) have a red number and their name as a caption under it.</item>
/// </list>
/// Everything is drawn from one function, <see cref="SkUiCalendar.DayStyle"/>, which the calendar asks once per day and remembers;
/// after the data changes, <see cref="SkUiCalendar.RefreshDayStyles"/> asks again. Tapping a day shows its agenda below the calendar,
/// where appointments and time off can be removed; the "New appointment" form adds to the chosen day.
/// </summary>
public sealed class TeamCalendarSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Team calendar (appointments, time off, holidays)",
        Summary: "A month calendar showing the app's own data under each day: appointments as colored dots, out-of-office days as an amber " +
                 "bar, public holidays as a red caption. Tap a day to see its agenda, add appointments to it, or book time off with a date range picker.",
        HowTo:
        [
            "Give the calendar a function that describes a day: `calendar.DayStyle = day => …` returns an `SkUiCalendarDayStyle` " +
                "(`TextColor`, `BackgroundColor`, `FontAttributes`, `IsStrikethrough`, `Markers`) or `null` for a plain day.",
            "Markers sit in a row under the number: `SkUiCalendarMarker.Dot(color)`, `Bar(color)`, `Caption(text, color)` or `Glyph(glyph, font, color)`.",
            "When the data behind the function changes, call `calendar.RefreshDayStyles()`: the days are asked again and redrawn.",
            "React to the chosen day with `SelectionChanged` (or bind `SelectedDate`), and move the page with `GoToDate(day)` or `GoToToday()`.",
            "Book a run of days with `SkUiDateRangePicker`: `Value` is an `SkUiDateRange` (`Start`, `LastDay`, `Days`), and `DisplayFormat` " +
                "formats the field (`{0}` first day, `{1}` last day, `{2}` number of days)."
        ],
        ThingsToKnow:
        [
            "The style function runs while drawing: keep it cheap (dictionary lookups, as here) and never change the data inside it.",
            "A day's markers share one row: what does not fit is left out, and a caption is shortened to fit, so keep holiday names short.",
            "Rows grow by the marker area (the look's 14 DIPs) once `DayStyle` is set; `DayMarkerAreaHeight` changes it.",
            "`VisibleWeeks = 1` turns the month into a weekly strip that pages by weeks, as the \"Week only\" switch does.",
            "For drawing of your own (avatars, prices, progress), use `DayPainter`: it draws each day with the calendar's text engine."
        ]);

    private const string Meeting = "Meeting";
    private const string CustomerCall = "Customer call";
    private const string Personal = "Personal";
    private const string Vacation = "Vacation";
    private const string Training = "Training";
    private const string Conference = "Conference";

    private static readonly Color MeetingColor = Color.FromArgb("#2563EB");
    private static readonly Color CustomerCallColor = Color.FromArgb("#16A34A");
    private static readonly Color PersonalColor = Color.FromArgb("#9333EA");
    private static readonly Color HolidayColor = Color.FromArgb("#DC2626");
    private static readonly Color OutOfOfficeColor = Color.FromArgb("#F59E0B");
    private static readonly Color OutOfOfficeBackground = Color.FromArgb("#FEF3C7");
    private static readonly Color WarningColor = Color.FromArgb("#B45309");

    private sealed record Appointment(DateTime Day, TimeSpan Time, string Title, string Category);

    private sealed record TimeOff(SkUiDateRange Range, string Reason);

    private readonly Dictionary<DateTime, string> _holidays = [];
    private readonly Dictionary<DateTime, List<Appointment>> _appointments = [];
    private readonly List<TimeOff> _timeOff = [];

    private readonly SkUiCalendar _calendar;
    private readonly SkUiLabel _dayTitle = new() { FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink };
    private readonly SkUiVerticalStackLayout _agenda = new() { Spacing = 8 };
    private readonly SkUiEntry _title = new() { Placeholder = "What is it?", FontSize = 15, TextColor = SampleColors.Ink };
    private readonly SkUiTimePicker _time = new() { Title = "Time", MinuteInterval = 15, Time = new TimeSpan(9, 0, 0), TextColor = SampleColors.Ink };
    private readonly SkUiPicker _category = new() { Title = "Category", ItemsSource = new[] { Meeting, CustomerCall, Personal }, SelectedIndex = 0, TextColor = SampleColors.Ink };
    private readonly SkUiLabel _appointmentStatus = new() { FontSize = 13, TextColor = SampleColors.Caption };
    private readonly SkUiDateRangePicker _away = new()
    {
        Title = "Away",
        Placeholder = "Choose the days",
        DisplayFormat = "{0:ddd d MMM} – {1:ddd d MMM} ({2} days)",
        UpdateMode = SkUiPickerUpdateMode.WhenFinished,
        IsPopupClearButtonVisible = true,
        TextColor = SampleColors.Ink
    };
    private readonly SkUiPicker _reason = new() { Title = "Reason", ItemsSource = new[] { Vacation, Training, Conference }, SelectedIndex = 0, TextColor = SampleColors.Ink };
    private readonly SkUiLabel _awayStatus = new() { FontSize = 13, TextColor = SampleColors.Caption };

    public TeamCalendarSample() : base(Info)
    {
        AddHolidays();
        AddExamples();
        var today = DateTime.Today;
        _calendar = new SkUiCalendar
        {
            FirstDayOfWeek = DayOfWeek.Monday,
            SelectedDate = today,
            HeaderLayout = SkUiCalendarHeaderLayout.Centered,
            DayNameFormat = SkUiCalendarDayNameFormat.Abbreviated,
            TodayStyle = SkUiCalendarTodayStyle.Ring,
            SelectionColor = SampleColors.Accent,
            TodayColor = SampleColors.Accent,
            NavigationButtonColor = SampleColors.Accent,
            TextColor = SampleColors.Ink,
            WeekendTextColor = SampleColors.Caption,
            DayStyle = StyleOf
        };
        _calendar.SelectionChanged += (_, _) => ShowDay();
        _away.MinimumDate = today.AddMonths(-1);

        var weekOnly = new SkUiSwitch { OnColor = SampleColors.Accent, VerticalOptions = LayoutOptions.Center };
        weekOnly.Toggled += (_, args) => _calendar.VisibleWeeks = args.Value ? 1 : 6;
        var todayButton = SmallButton("Today", () =>
        {
            _calendar.SelectedDate = DateTime.Today;
            _calendar.GoToToday();
        });

        var add = new SkUiButton { Text = "Add to the chosen day", FillColor = SampleColors.Accent, CornerRadius = 8 };
        add.Clicked += (_, _) => AddAppointment();
        var addAway = new SkUiButton { Text = "Add time off", FillColor = SampleColors.Accent, CornerRadius = 8 };
        addAway.Clicked += (_, _) => AddTimeOff();

        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                Card(
                    new SkUiHorizontalStackLayout
                    {
                        Spacing = 8,
                        Children = { todayButton, new SkUiLabel { Text = "Week only", FontSize = 14, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center }, weekOnly }
                    },
                    _calendar,
                    Legend()),
                Card(_dayTitle, _agenda),
                Card(Heading("New appointment"), _title, Row(_time, _category), add, _appointmentStatus),
                Card(Heading("Out of office"), _away, _reason, addAway, _awayStatus)
            }
        };
        ShowDay();
    }

    /// <summary>How the calendar draws a day: asked once per day while drawing, again after <see cref="SkUiCalendar.RefreshDayStyles"/>.</summary>
    private SkUiCalendarDayStyle? StyleOf(DateTime day)
    {
        var holiday = _holidays.GetValueOrDefault(day);
        var away = AwayOn(day) is not null;
        var appointments = _appointments.GetValueOrDefault(day);
        if (holiday is null && !away && appointments is not { Count: > 0 })
            return null;
        List<SkUiCalendarMarker> markers = [];
        if (holiday is not null)
            markers.Add(SkUiCalendarMarker.Caption(holiday, HolidayColor)); // a holiday's name says enough
        else
        {
            if (away)
                markers.Add(SkUiCalendarMarker.Bar(OutOfOfficeColor));
            if (appointments is not null)
                markers.AddRange(appointments.Take(3).Select(appointment => SkUiCalendarMarker.Dot(ColorOf(appointment.Category))));
        }
        return new SkUiCalendarDayStyle
        {
            TextColor = holiday is not null ? HolidayColor : null,
            FontAttributes = holiday is not null ? FontAttributes.Bold : null,
            BackgroundColor = away ? OutOfOfficeBackground : null,
            Markers = markers
        };
    }

    /// <summary>The chosen day's agenda: its holiday, the time off it falls in, and its appointments by time.</summary>
    private void ShowDay()
    {
        var day = _calendar.SelectedDate ?? DateTime.Today;
        _dayTitle.Text = day.ToString("D", CultureInfo.CurrentCulture);
        _agenda.Children.Clear();
        if (_holidays.TryGetValue(day, out var holiday))
            _agenda.Children.Add(AgendaRow(HolidayColor, "Public holiday", holiday, remove: null));
        if (AwayOn(day) is { } away)
        {
            var when = away.Range.Days == 1 ? "All day" : $"{away.Range.Start:d MMM} – {away.Range.LastDay:d MMM}";
            _agenda.Children.Add(AgendaRow(OutOfOfficeColor, when, $"Out of office: {away.Reason}", () =>
            {
                _timeOff.Remove(away);
                Changed();
            }));
        }
        foreach (var appointment in (_appointments.GetValueOrDefault(day) ?? []).OrderBy(appointment => appointment.Time))
        {
            var time = DateTime.Today.Add(appointment.Time).ToString("t", CultureInfo.CurrentCulture);
            _agenda.Children.Add(AgendaRow(ColorOf(appointment.Category), time, $"{appointment.Title} · {appointment.Category}", () =>
            {
                _appointments[appointment.Day].Remove(appointment);
                Changed();
            }));
        }
        if (_agenda.Children.Count == 0)
            _agenda.Children.Add(new SkUiLabel { Text = "Nothing planned.", FontSize = 14, TextColor = SampleColors.Caption });
    }

    private void AddAppointment()
    {
        var day = _calendar.SelectedDate ?? DateTime.Today;
        if (string.IsNullOrWhiteSpace(_title.Text))
        {
            _appointmentStatus.Text = "Give the appointment a title first.";
            _appointmentStatus.TextColor = WarningColor;
            return;
        }
        Add(new Appointment(day, _time.Time ?? new TimeSpan(9, 0, 0), _title.Text.Trim(), (string?)_category.SelectedItem ?? Meeting));
        // The calendar still shows the day: tell the app when it lands on a holiday or on time off.
        var clash = _holidays.TryGetValue(day, out var holiday) ? $" Note: it is {holiday}, a public holiday."
            : AwayOn(day) is { } away ? $" Note: you are out of office ({away.Reason})." : string.Empty;
        _appointmentStatus.Text = $"Added to {day:ddd d MMM}.{clash}";
        _appointmentStatus.TextColor = clash.Length > 0 ? WarningColor : SampleColors.Caption;
        _title.Text = string.Empty;
        Changed();
    }

    private void AddTimeOff()
    {
        if (_away.Value is not { IsComplete: true } range)
        {
            _awayStatus.Text = "Choose the first and the last day first.";
            _awayStatus.TextColor = WarningColor;
            return;
        }
        // Time off that overlaps the new one is replaced by it.
        _timeOff.RemoveAll(entry => entry.Range.Start <= range.LastDay && range.Start <= entry.Range.LastDay);
        _timeOff.Add(new TimeOff(range, (string?)_reason.SelectedItem ?? Vacation));
        var clashes = _appointments.Where(pair => range.Contains(pair.Key)).Sum(pair => pair.Value.Count);
        var workDays = Enumerable.Range(0, range.Days).Select(offset => range.Start.AddDays(offset))
            .Count(day => day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !_holidays.ContainsKey(day));
        _awayStatus.Text = $"{range.Days} days away, {workDays} of them work days." +
                           (clashes > 0 ? $" {clashes} appointment(s) fall in this time." : string.Empty);
        _awayStatus.TextColor = clashes > 0 ? WarningColor : SampleColors.Caption;
        _away.Value = null;
        _calendar.SelectedDate = range.Start;
        _calendar.GoToDate(range.Start);
        Changed();
    }

    /// <summary>The data changed: the calendar asks its days again, and the agenda follows.</summary>
    private void Changed()
    {
        _calendar.RefreshDayStyles();
        ShowDay();
    }

    private TimeOff? AwayOn(DateTime day) => _timeOff.FirstOrDefault(entry => entry.Range.Contains(day));

    private void Add(Appointment appointment)
    {
        if (!_appointments.TryGetValue(appointment.Day, out var list))
            _appointments[appointment.Day] = list = [];
        list.Add(appointment);
    }

    /// <summary>Public holidays of a made-up country, for last year, this year and next year.</summary>
    private void AddHolidays()
    {
        var year = DateTime.Today.Year;
        for (var y = year - 1; y <= year + 1; y++)
        {
            _holidays[new DateTime(y, 1, 1)] = "New Year";
            _holidays[new DateTime(y, 3, 14)] = "Founders";
            _holidays[new DateTime(y, 5, 8)] = "Blossom";
            _holidays[new DateTime(y, 6, 21)] = "Midsummer";
            _holidays[new DateTime(y, 9, 23)] = "Harvest";
            _holidays[new DateTime(y, 11, 20)] = "Lanterns";
            _holidays[new DateTime(y, 12, 25)] = "Winter";
            _holidays[new DateTime(y, 12, 26)] = "Winter";
            // A holiday on the last Monday of August.
            var august = new DateTime(y, 8, 31);
            _holidays[august.AddDays(-(((int)august.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7))] = "Summer";
        }
    }

    /// <summary>Some appointments and time off around today, so the page has something to show.</summary>
    private void AddExamples()
    {
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        for (var week = -2; week <= 6; week++)
        {
            var start = monday.AddDays(week * 7);
            Add(new Appointment(start, new TimeSpan(9, 30, 0), "Team stand-up", Meeting));
            Add(new Appointment(start.AddDays(2), new TimeSpan(14, 0, 0), "Planning", Meeting));
            if (week % 2 == 0)
                Add(new Appointment(start.AddDays(3), new TimeSpan(11, 0, 0), "Call with Northwind", CustomerCall));
        }
        Add(new Appointment(DateTime.Today, new TimeSpan(10, 30, 0), "Design review", Meeting));
        Add(new Appointment(DateTime.Today, new TimeSpan(17, 0, 0), "Gym", Personal));
        Add(new Appointment(monday.AddDays(2), new TimeSpan(16, 0, 0), "Contoso demo", CustomerCall));
        Add(new Appointment(monday.AddDays(2), new TimeSpan(18, 30, 0), "Dentist", Personal));
        Add(new Appointment(monday.AddDays(4), new TimeSpan(12, 0, 0), "Lunch with Sam", Personal));
        _timeOff.Add(new TimeOff(new SkUiDateRange(monday.AddDays(14), monday.AddDays(18)), Vacation));
        _timeOff.Add(new TimeOff(new SkUiDateRange(monday.AddDays(-4), monday.AddDays(-3)), Training));
    }

    private static Color ColorOf(string category) => category switch
    {
        CustomerCall => CustomerCallColor,
        Personal => PersonalColor,
        _ => MeetingColor
    };

    private static SkUiGrid AgendaRow(Color color, string when, string what, Action? remove)
    {
        var row = new SkUiGrid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = [new ColumnDefinition(4), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Children =
            {
                new SkUiBorder { BackgroundColor = color, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 2 }, MinimumHeightRequest = 32 },
                Cell(new SkUiLabel { Text = when, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center }, 1),
                Cell(new SkUiLabel { Text = what, FontSize = 14, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap }, 2)
            }
        };
        if (remove is not null)
            row.Children.Add(Cell(SmallButton("Remove", remove), 3));
        return row;
    }

    private static SkUiView Cell(SkUiView view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    private static SkUiButton SmallButton(string text, Action clicked)
    {
        var button = new SkUiButton
        {
            Text = text,
            FontSize = 13,
            TextColor = SampleColors.Accent,
            FillColor = Colors.Transparent,
            BorderColor = SampleColors.Border,
            BorderWidth = 1,
            CornerRadius = 8,
            Padding = new Thickness(12, 4),
            VerticalOptions = LayoutOptions.Center
        };
        button.Clicked += (_, _) => clicked();
        return button;
    }

    private static SkUiView Legend()
    {
        var legend = new SkUiWrapLayout { Spacing = 14, RowSpacing = 6 };
        foreach (var (color, text) in new[] { (MeetingColor, Meeting), (CustomerCallColor, CustomerCall), (PersonalColor, Personal), (OutOfOfficeColor, "Out of office"), (HolidayColor, "Holiday") })
            legend.Children.Add(new SkUiHorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new SkUiBorder { BackgroundColor = color, StrokeThickness = 0, StrokeShape = new Ellipse(), WidthRequest = 8, HeightRequest = 8, VerticalOptions = LayoutOptions.Center },
                    new SkUiLabel { Text = text, FontSize = 12, TextColor = SampleColors.Caption, VerticalOptions = LayoutOptions.Center }
                }
            });
        return legend;
    }

    private static SkUiGrid Row(SkUiView first, SkUiView second)
    {
        Grid.SetColumn(second, 1);
        return new SkUiGrid
        {
            ColumnSpacing = 10,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            Children = { first, second }
        };
    }

    private static SkUiBorder Card(params SkUiView[] children)
    {
        var stack = new SkUiVerticalStackLayout { Spacing = 10 };
        foreach (var child in children)
            stack.Children.Add(child);
        return new SkUiBorder
        {
            BackgroundColor = SampleColors.Surface,
            Stroke = SampleColors.Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14),
            Content = stack
        };
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
