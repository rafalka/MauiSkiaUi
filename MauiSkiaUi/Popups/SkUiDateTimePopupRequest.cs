namespace MauiSkiaUi;

/// <summary>
/// The request of a date picker (<see cref="SkUiDatePicker"/>, <see cref="SkUiDateTimePicker"/> and their Core twins): the date
/// and its range, for presenters that show their own date UI (a platform dialog, a calendar of their own) instead of
/// <see cref="SkUiPopupRequest.Content"/>'s wheels. Such a presenter shows <see cref="Date"/> (or
/// <see cref="DefaultHighlightedDate"/> while there is none) between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>,
/// offers the days <see cref="IsDateEnabled"/> allows, and ends with <see cref="CompleteDate"/>, <see cref="SkUiPopupRequest.Clear"/>
/// or <see cref="SkUiPopupRequest.Cancel"/>. As a <see cref="SkUiColumnsPopupRequest"/> it also describes the wheels.
/// </summary>
public sealed class SkUiDatePopupRequest : SkUiColumnsPopupRequest
{
    private SkUiDateTimeWheels? _wheels;

    internal SkUiDatePopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, SkUiDateTimeWheels wheels,
        IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply)
        : base(anchor, platformAnchor, content, layout, wheels.Texts.Select(texts => (IReadOnlyList<string>)texts).ToArray(), wheels.Loops, selected, apply,
            wheels.Headers)
    {
        _wheels = wheels;
        Date = wheels.Value;
        MinimumDate = wheels.Minimum;
        MaximumDate = wheels.Maximum;
        DefaultHighlightedDate = wheels.Highlighted;
        IncludesTime = wheels.Kind == SkUiDateTimeKind.DateAndTime;
        HourInterval = wheels.HourInterval;
        MinuteInterval = wheels.MinuteInterval;
        SecondInterval = wheels.SecondInterval;
        Is24Hour = wheels.Is24Hour;
    }

    /// <summary>The picker's date when the popup opened (with its time for a date and time picker); <c>null</c>: none.</summary>
    public DateTime? Date { get; }

    /// <summary>The date the popup has chosen now (the draft until OK with <see cref="SkUiPopupRequest.RequiresAccept"/>); <c>null</c> while nothing is chosen, and once the request completed.</summary>
    public DateTime? CurrentDate => _wheels?.ValueOf(SelectedIndices);

    /// <summary>The first date that can be chosen (the picker's <c>MinimumDate</c>, on the wheels' steps).</summary>
    public DateTime MinimumDate { get; }

    /// <summary>The last date that can be chosen (the picker's <c>MaximumDate</c>, on the wheels' steps).</summary>
    public DateTime MaximumDate { get; }

    /// <summary>What to show while <see cref="Date"/> is <c>null</c> (the picker's <c>DefaultHighlightedDate</c>, else today or now, in the range).</summary>
    public DateTime DefaultHighlightedDate { get; }

    /// <summary>Whether the picker chooses a time of the day too (<see cref="SkUiDateTimePicker"/>).</summary>
    public bool IncludesTime { get; }

    /// <summary>The hour wheel's step (a date and time picker's <c>HourInterval</c>; 1 for a date picker).</summary>
    public int HourInterval { get; }

    /// <summary>The minute wheel's step.</summary>
    public int MinuteInterval { get; }

    /// <summary>The second wheel's step.</summary>
    public int SecondInterval { get; }

    /// <summary>Whether a time is shown on a 24-hour clock (the wheel format's, else the culture's).</summary>
    public bool Is24Hour { get; }

    /// <summary>Whether <paramref name="date"/>'s day can be chosen: in the range, not one of the picker's <c>BlackoutDates</c>, allowed by its <c>DateEnabled</c>. <c>false</c> once the request completed.</summary>
    public bool IsDateEnabled(DateTime date) => _wheels?.IsDateEnabled(date) == true;

    /// <summary>
    /// Changes the chosen date (see <see cref="SkUiSelectionPopupRequest.SetSelection"/>: applied at once, or the draft OK applies);
    /// <c>null</c> chooses none. The date is rounded down to the wheels' steps (a time between two minutes of the minute wheel to
    /// the earlier). Returns <c>false</c> (and changes nothing) for a date that cannot be chosen, or when the popup is closing.
    /// </summary>
    public bool SetDate(DateTime? date)
    {
        if (_wheels is not { } wheels)
            return false;
        if (date is not { } value)
            return SetSelection(EmptySelection);
        return wheels.IndicesOfChoice(value) is { } indices && Choose(wheels, indices);
    }

    /// <summary>
    /// Chooses <paramref name="date"/> (rounded as <see cref="SetDate"/>; <c>null</c>: none) and closes the popup: what a presenter
    /// with its own UI calls when the user picked a date. Returns <c>false</c> for a date that cannot be chosen, or when the request
    /// already has a result, was closed or completed.
    /// </summary>
    public bool CompleteDate(DateTime? date)
    {
        if (_wheels is not { } wheels)
            return false;
        if (date is not { } value)
            return Complete(EmptySelection);
        return wheels.IndicesOfChoice(value) is { } indices && CompleteWith(wheels, indices);
    }

    /// <inheritdoc />
    private protected override void OnCompleted()
    {
        _wheels = null;
        base.OnCompleted();
    }
}

/// <summary>
/// The request of a time picker (<see cref="SkUiTimePicker"/>, <see cref="Core.SkUiCoreTimePicker"/>): the time and its range,
/// for presenters that show their own time UI (a platform dialog) instead of <see cref="SkUiPopupRequest.Content"/>'s wheels. Such
/// a presenter shows <see cref="Time"/> (or <see cref="DefaultHighlightedTime"/>) between <see cref="MinimumTime"/> and
/// <see cref="MaximumTime"/>, on <see cref="Is24Hour"/>'s clock, and ends with <see cref="CompleteTime"/>,
/// <see cref="SkUiPopupRequest.Clear"/> or <see cref="SkUiPopupRequest.Cancel"/>.
/// </summary>
public sealed class SkUiTimePopupRequest : SkUiColumnsPopupRequest
{
    private SkUiDateTimeWheels? _wheels;

    internal SkUiTimePopupRequest(View anchor, View? platformAnchor, View? content, in SkUiPopupLayout layout, SkUiDateTimeWheels wheels,
        IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply)
        : base(anchor, platformAnchor, content, layout, wheels.Texts.Select(texts => (IReadOnlyList<string>)texts).ToArray(), wheels.Loops, selected, apply,
            wheels.Headers)
    {
        _wheels = wheels;
        Time = wheels.Value?.TimeOfDay;
        MinimumTime = wheels.Minimum.TimeOfDay;
        MaximumTime = wheels.Maximum.TimeOfDay;
        DefaultHighlightedTime = wheels.Highlighted.TimeOfDay;
        Is24Hour = wheels.Is24Hour;
        IncludesSeconds = wheels.IncludesSeconds;
        HourInterval = wheels.HourInterval;
        MinuteInterval = wheels.MinuteInterval;
        SecondInterval = wheels.SecondInterval;
    }

    /// <summary>The picker's time when the popup opened; <c>null</c>: none.</summary>
    public TimeSpan? Time { get; }

    /// <summary>The time the popup has chosen now (the draft until OK with <see cref="SkUiPopupRequest.RequiresAccept"/>); <c>null</c> while nothing is chosen, and once the request completed.</summary>
    public TimeSpan? CurrentTime => _wheels?.ValueOf(SelectedIndices)?.TimeOfDay;

    /// <summary>The first time that can be chosen (the picker's <c>MinimumTime</c>, on the wheels' steps).</summary>
    public TimeSpan MinimumTime { get; }

    /// <summary>The last time that can be chosen (the picker's <c>MaximumTime</c>, on the wheels' steps).</summary>
    public TimeSpan MaximumTime { get; }

    /// <summary>What to show while <see cref="Time"/> is <c>null</c> (the picker's <c>DefaultHighlightedTime</c>, else now, in the range).</summary>
    public TimeSpan DefaultHighlightedTime { get; }

    /// <summary>Whether the time is shown on a 24-hour clock (the wheel format's, else the culture's).</summary>
    public bool Is24Hour { get; }

    /// <summary>Whether the wheels choose seconds (else a whole minute).</summary>
    public bool IncludesSeconds { get; }

    /// <summary>The hour wheel's step (the picker's <c>HourInterval</c>).</summary>
    public int HourInterval { get; }

    /// <summary>The minute wheel's step (the picker's <c>MinuteInterval</c>).</summary>
    public int MinuteInterval { get; }

    /// <summary>The second wheel's step (the picker's <c>SecondInterval</c>).</summary>
    public int SecondInterval { get; }

    /// <summary>
    /// Changes the chosen time (applied at once, or the draft OK applies); <c>null</c> chooses none. Rounded down to the wheels'
    /// steps. Returns <c>false</c> (and changes nothing) for a time outside the range or of a day's 24 hours, or when the popup is closing.
    /// </summary>
    public bool SetTime(TimeSpan? time)
    {
        if (_wheels is not { } wheels)
            return false;
        if (time is not { } value)
            return SetSelection(EmptySelection);
        return IsTimeOfDay(value) && wheels.IndicesOfChoice(SkUiDateTimeWheels.TimeDate + value) is { } indices && Choose(wheels, indices);
    }

    /// <summary>Chooses <paramref name="time"/> (rounded as <see cref="SetTime"/>; <c>null</c>: none) and closes the popup. Returns <c>false</c> for a time that cannot be chosen, or when the request already has a result, was closed or completed.</summary>
    public bool CompleteTime(TimeSpan? time)
    {
        if (_wheels is not { } wheels)
            return false;
        if (time is not { } value)
            return Complete(EmptySelection);
        return IsTimeOfDay(value) && wheels.IndicesOfChoice(SkUiDateTimeWheels.TimeDate + value) is { } indices && CompleteWith(wheels, indices);
    }

    private static bool IsTimeOfDay(TimeSpan time) => time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);

    /// <inheritdoc />
    private protected override void OnCompleted()
    {
        _wheels = null;
        base.OnCompleted();
    }
}
