using System.Globalization;
using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: wheels whose items depend on each other. A date of birth on three wheels (day, month, year): when the month or the
/// year changes, the days that month does not have can no longer be chosen. Each day is an item with an <c>IsValid</c> flag,
/// the day column's <see cref="SkUiPickerColumn.ItemEnabledBinding"/> reads it, and <see cref="SkUiPickerColumn.RefreshItemStates"/>
/// tells the picker after the flags changed: the dimmed days are passed by, and an open wheel on the 31st of a month that
/// turned into February rolls to the 28th (the 29th in a leap year).
/// <para>
/// The month names come from the current culture; the field joins day, month and year (<c>Format</c>), and the page shows
/// the age.
/// </para>
/// </summary>
public sealed class DateOfBirthSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Date of birth (wheels that depend on each other)",
        Summary: "Day, month and year on wheels: when the month or year changes, the days it does not have are dimmed and passed by, " +
                 "and a wheel on a day that is gone rolls to the last one.",
        HowTo:
        [
            "Give each item what decides whether it can be chosen (`DayItem.IsValid`) and point the column at it: " +
                "`ItemEnabledBinding = Binding.Create(static (DayItem day) => day.IsValid)`.",
            "When the other columns change (`SelectionChanged` names the column), update the flags and call " +
                "`column.RefreshItemStates()`; an open popup follows at once.",
            "Show items of your own type with `ItemDisplay` (a function), e.g. `item => ((DayItem)item!).Number.ToString()`.",
            "Join the field's text with `Format = \"{0} {1} {2}\"`, and name the wheels with `Header`."
        ],
        ThingsToKnow:
        [
            "This page uses `UpdateMode = Immediately`: the month applies as its wheel turns, so the days follow while the popup is open. " +
                "With `WhenFinished` the wheels are a draft until OK, and the days would follow after it.",
            "A wheel that stops on a dimmed day rolls on to the nearest one that can be chosen; the keyboard and taps pass them by.",
            "The app may still select a day that cannot be chosen; this page moves the choice to the last valid day itself when the " +
                "popup is closed.",
            "C7's date picker will be built on these wheels."
        ]);

    /// <summary>A day of the month, which the month shown may not have.</summary>
    public sealed class DayItem(int number)
    {
        public int Number { get; } = number;

        public bool IsValid { get; set; } = true;
    }

    private readonly List<DayItem> _dayItems = [.. Enumerable.Range(1, 31).Select(day => new DayItem(day))];
    private readonly SkUiPickerColumn _days;
    private readonly SkUiPickerColumn _months;
    private readonly SkUiPickerColumn _years;
    private readonly SkUiMultiColumnPicker _date;
    private readonly SkUiLabel _summary = new() { FontSize = 15, TextColor = SampleColors.Ink };

    public DateOfBirthSample() : base(Info)
    {
        var monthNames = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Where(name => name.Length > 0).ToList();
        _days = new SkUiPickerColumn
        {
            Header = "Day",
            ItemsSource = _dayItems,
            ItemDisplay = item => ((DayItem)item!).Number.ToString(CultureInfo.CurrentCulture),
            ItemEnabledBinding = Binding.Create(static (DayItem day) => day.IsValid),
            SelectedIndex = 14
        };
        _months = new SkUiPickerColumn { Header = "Month", ItemsSource = monthNames, SelectedIndex = 5 };
        _years = SkUiPickerColumn.Range(1920, DateTime.Today.Year).SetHeader("Year");
        _years.SelectedItem = 1990;
        _date = new SkUiMultiColumnPicker
        {
            Title = "Date of birth",
            Placeholder = "Choose your date of birth",
            Format = "{0} {1} {2}",
            PopupPlacement = SkUiPopupPlacement.Window,
            TextColor = SampleColors.Ink,
            Columns = { _days, _months, _years }
        };
        _date.SelectionChanged += (_, args) =>
        {
            // The month or the year changed: the days follow.
            if (args.ColumnIndex != 0)
                UpdateDays();
            UpdateSummary();
        };
        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { Heading("Date of birth"), _date, _summary }
        };
        UpdateDays();
        UpdateSummary();
    }

    /// <summary>The days the chosen month has (a leap year until a year is chosen): the others cannot be chosen.</summary>
    private void UpdateDays()
    {
        var year = _years.SelectedItem as int? ?? 2000;
        var month = Math.Max(0, _months.SelectedIndex) + 1;
        var count = DateTime.DaysInMonth(year, month);
        foreach (var day in _dayItems)
            day.IsValid = day.Number <= count;
        _days.RefreshItemStates();
        // A closed picker keeps a choice that became invalid: move it to the last day.
        if (_days.SelectedIndex >= count)
            _days.SelectedIndex = count - 1;
    }

    private void UpdateSummary()
    {
        if (_days.SelectedItem is not DayItem day || _months.SelectedIndex < 0 || _years.SelectedItem is not int year)
        {
            _summary.Text = "Choose a day, a month and a year.";
            return;
        }
        // The app (not the user) may still have chosen a day the month does not have.
        if (day.Number > DateTime.DaysInMonth(year, _months.SelectedIndex + 1))
        {
            _summary.Text = "That month does not have that day.";
            return;
        }
        var birth = new DateTime(year, _months.SelectedIndex + 1, day.Number);
        var today = DateTime.Today;
        var age = today.Year - birth.Year - (today < birth.AddYears(today.Year - birth.Year) ? 1 : 0);
        _summary.Text = birth > today ? "That date is in the future." : $"{birth.ToString("D", CultureInfo.CurrentCulture)}: {age} years old.";
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
