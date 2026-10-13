using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls.Pickers;

/// <summary>
/// HOWTO: a time on wheels, and a few smaller pickers around it. <see cref="SkUiMultiColumnPicker"/> holds two looping columns
/// of numbers in steps (<see cref="SkUiPickerColumn.Range"/>: hours, minutes every five) with headers; <c>Format</c> joins the
/// field's text ("07:30"). The time waits for OK (<c>UpdateMode = WhenFinished</c>), and the popup's Clear button turns the
/// alarm off: the field then shows its placeholder.
/// <para>
/// The days repeat on a picker with several choices ("Every day" is its select-all row) and the snooze length on a narrow
/// picker whose list is given a minimum width, so a list of short numbers does not open as wide as a dialog.
/// </para>
/// </summary>
public sealed class AlarmClockSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Alarm clock (time on wheels)",
        Summary: "A time chosen on looping wheels of hours and minutes in steps, with headers, OK / Cancel and a Clear that turns the " +
                 "alarm off; the days it repeats on with several choices; a narrow snooze picker with a minimum list width.",
        HowTo:
        [
            "Columns of numbers: `SkUiPickerColumn.Range(0, 55, 5, \"00\")` (from, to, step, a number format); its items are " +
                "`int`s, so `SelectedItem = 30` works.",
            "Make them wheels of a clock: `Loop = true`, and name them with `Header`.",
            "Join the field's text with `Format = \"{0}:{1}\"` (or `Separator`).",
            "Wait for OK: `UpdateMode = SkUiPickerUpdateMode.WhenFinished`; add a Clear button with `IsPopupClearButtonVisible`, and a " +
                "`Placeholder` the field shows when nothing is chosen.",
            "A list of short items: `MinPopupWidth` gives it a width of its own (a window popup is otherwise the look's 320).",
            "Items show their `ToString()` without an `ItemDisplayBinding` (the snooze lengths are records with one)."
        ],
        ThingsToKnow:
        [
            "Each wheel's change applies to its column at once with `Immediately`; with `WhenFinished` the wheels are a draft until OK.",
            "A tap on an item scrolls it to the middle and chooses it; the keyboard moves the active wheel (Left / Right change it).",
            "The wheels always show a time: after Clear, moving one wheel (or OK) chooses what both show, never just an hour.",
            "Clear (\"No alarm\") closes the popup: the wheels cannot show that nothing is chosen.",
            "`SelectAllText` names the select-all row (\"Every day\").",
            "The popup's texts (OK, Cancel, Clear) come from `SkUiStrings` unless the picker sets its own."
        ]);

    private static readonly string[] Days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    /// <summary>A snooze length; its text is the list's and the field's (no display binding needed).</summary>
    public sealed record Snooze(int Minutes)
    {
        public override string ToString() => $"{Minutes} min";
    }

    private static readonly Snooze[] SnoozeLengths = [new(1), new(3), new(5), new(10), new(15), new(20), new(30)];

    private readonly SkUiPickerColumn _hours = SkUiPickerColumn.Range(0, 23, 1, "00").SetHeader("Hour").SetLoop(true);
    private readonly SkUiPickerColumn _minutes = SkUiPickerColumn.Range(0, 55, 5, "00").SetHeader("Minute").SetLoop(true);
    private readonly SkUiMultiColumnPicker _time;
    private readonly SkUiPicker _repeat;
    private readonly SkUiPicker _snooze;
    private readonly SkUiLabel _summary = new() { FontSize = 15, TextColor = SampleColors.Ink };

    public AlarmClockSample() : base(Info)
    {
        _hours.SelectedItem = 7;
        _minutes.SelectedItem = 30;
        _time = new SkUiMultiColumnPicker
        {
            Title = "Alarm",
            Placeholder = "No alarm",
            Format = "{0}:{1}",
            UpdateMode = SkUiPickerUpdateMode.WhenFinished,
            IsPopupClearButtonVisible = true,
            ClearText = "No alarm",
            PopupPlacement = SkUiPopupPlacement.Window,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = SampleColors.Ink,
            Columns = { _hours, _minutes }
        };
        _repeat = new SkUiPicker
        {
            Title = "Repeat",
            Placeholder = "Once",
            ItemsSource = Days,
            SelectionMode = SkUiSelectionMode.Multiple,
            IsSelectAllVisible = true,
            SelectAllText = "Every day",
            SelectedCountFormat = "{0} days",
            PopupPlacement = SkUiPopupPlacement.Anchored,
            TextColor = SampleColors.Ink
        };
        foreach (var day in Days.Take(5))
            _repeat.SelectedItems.Add(day);
        _snooze = new SkUiPicker
        {
            Title = "Snooze",
            ItemsSource = SnoozeLengths,
            SelectedIndex = 4,
            HorizontalOptions = LayoutOptions.Start,
            PopupPlacement = SkUiPopupPlacement.Window,
            MinPopupWidth = 140,
            TextColor = SampleColors.Ink
        };
        _time.SelectionChanged += (_, _) => UpdateSummary();
        _repeat.SelectionChanged += (_, _) => UpdateSummary();
        _snooze.SelectedIndexChanged += (_, _) => UpdateSummary();
        SampleContent = new SkUiVerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(16),
            BackgroundColor = SampleColors.Surface,
            Children = { Heading("Time"), _time, Heading("Repeat"), _repeat, Heading("Snooze"), _snooze, _summary }
        };
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (_hours.SelectedItem is not int hour || _minutes.SelectedItem is not int minute)
        {
            _summary.Text = "The alarm is off.";
            return;
        }
        var days = _repeat.SelectedIndices.Count switch
        {
            0 => "once",
            7 => "every day",
            _ => "on " + string.Join(", ", _repeat.SelectedIndices.Select(index => Days[index][..3]))
        };
        _summary.Text = $"Rings at {hour:00}:{minute:00} {days}; snoozes for {(_snooze.SelectedItem as Snooze)?.Minutes} minutes.";
    }

    private static SkUiLabel Heading(string text) =>
        new() { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = SampleColors.Caption, TextTransform = TextTransform.Uppercase };
}
