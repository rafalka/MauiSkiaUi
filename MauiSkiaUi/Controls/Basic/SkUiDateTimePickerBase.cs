namespace MauiSkiaUi;

/// <summary>
/// What the drawn date and time pickers share (<see cref="SkUiDatePicker"/>, <see cref="SkUiTimePicker"/>,
/// <see cref="SkUiDateTimePicker"/>): the field and popup of every picker (<see cref="SkUiPickerBase"/>), and a popup of wheels
/// for the parts of a date or a time (day, month, year; hour, minute, second, AM / PM), ordered, named and formatted by the
/// current culture or by <see cref="WheelFormat"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing.</b> Turning a wheel, tapping an item or the keyboard changes the value at once (or, with
/// <see cref="SkUiPickerBase.UpdateMode"/> <see cref="SkUiPickerUpdateMode.WhenFinished"/>, when OK is pressed). The wheels depend on
/// each other: the days a month does not have, and the parts outside the range, are dimmed and passed by; a choice that cannot be
/// made moves to the nearest one that can (the 31st of a month turned February to its last day, a date before the minimum to the
/// minimum), and a wheel that stops on a dimmed item rolls there.
/// </para>
/// <para>
/// <b>Keyboard</b> (in the popup): Left / Right change the wheel, Up / Down, Page Up / Page Down, Home / End move it, typing jumps
/// to the next item starting with what was typed, Enter or Space accept, Escape cancels.
/// </para>
/// </remarks>
public abstract class SkUiDateTimePickerBase : SkUiPickerBase
{
    private protected SkUiDateTimePickerBase(SkUiDateTimeKind kind) => Dates = new SkUiDateTimeModel(Model, kind, value => ChooseValue(value));

    /// <summary>The engine of the field's text and the wheels.</summary>
    private protected SkUiDateTimeModel Dates { get; }

    /// <summary>The popup chose <paramref name="value"/> (<c>null</c>: cleared): the control's value takes it, by its rules.</summary>
    private protected abstract void ChooseValue(DateTime? value);

    /// <summary>Bindable property for <see cref="WheelFormat"/>.</summary>
    public static readonly BindableProperty WheelFormatProperty = BindableProperty.Create(nameof(WheelFormat), typeof(string), typeof(SkUiDateTimePickerBase), null,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePickerBase)picker).ApplyWheels(dates => dates.WheelFormat = (string?)value));

    /// <summary>Bindable property for <see cref="IsLooping"/>.</summary>
    public static readonly BindableProperty IsLoopingProperty = BindableProperty.Create(nameof(IsLooping), typeof(bool), typeof(SkUiDateTimePickerBase), true,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePickerBase)picker).ApplyWheels(dates => dates.Loop = (bool)value));

    /// <summary>Bindable property for <see cref="AreColumnHeadersVisible"/>.</summary>
    public static readonly BindableProperty AreColumnHeadersVisibleProperty = BindableProperty.Create(nameof(AreColumnHeadersVisible), typeof(bool),
        typeof(SkUiDateTimePickerBase), false,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePickerBase)picker).ApplyWheels(dates => dates.ShowsHeaders = (bool)value));

    /// <summary>Bindable property for <see cref="VisibleRows"/>.</summary>
    public static readonly BindableProperty VisibleRowsProperty = BindableProperty.Create(nameof(VisibleRows), typeof(int), typeof(SkUiDateTimePickerBase),
        SkUiDateTimeModel.DefaultVisibleRows, validateValue: (_, value) => (int)value is >= 1 and <= SkUiColumnsModel.MaximumVisibleRows,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePickerBase)picker).ApplyWheels(dates => dates.VisibleRows = (int)value));

    /// <summary>
    /// Which wheels the popup shows, in which order and how: a .NET date and time format, standard (<c>"d"</c>, <c>"t"</c>,
    /// <c>"g"</c>, <c>"Y"</c> for a month and a year) or custom, whose parts name the wheels: <c>d</c> / <c>dd</c> the day,
    /// <c>M</c> / <c>MM</c> the month's number, <c>MMM</c> / <c>MMMM</c> its name, <c>yy</c> / <c>yyyy</c> the year, <c>H</c> /
    /// <c>HH</c> a 24-hour clock's hour, <c>h</c> / <c>hh</c> a 12-hour clock's (with an AM / PM wheel, <c>tt</c>, added when the
    /// format has none), <c>mm</c> the minutes, <c>ss</c> the seconds; the rest (separators, a weekday) is ignored, and a part the
    /// picker does not choose (a time in a date picker) too. <c>"MMMM yyyy"</c> makes a month picker, <c>"MM/yy"</c> a card's expiry.
    /// A part without a wheel keeps the value's when it is above the smallest wheel (a time picker's date), and is its least below it
    /// (a month picker chooses the 1st). <c>null</c> (default): the culture's short date order with month names, and its short time.
    /// SkiaUi extension.
    /// </summary>
    public string? WheelFormat { get => (string?)GetValue(WheelFormatProperty); set => SetValue(WheelFormatProperty, value); }

    /// <summary>Whether the wheels of the month, the day, the hour, the minute and the second loop (default <c>true</c>; years and AM / PM never do). SkiaUi extension.</summary>
    public bool IsLooping { get => (bool)GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }

    /// <summary>Whether the wheels have headers ("Day", "Month", "Year", "Hour", …; <see cref="SkUiStrings"/>' texts). Default <c>false</c>. SkiaUi extension.</summary>
    public bool AreColumnHeadersVisible { get => (bool)GetValue(AreColumnHeadersVisibleProperty); set => SetValue(AreColumnHeadersVisibleProperty, value); }

    /// <summary>How many rows of each wheel show (default 5; 1–9). SkiaUi extension.</summary>
    public int VisibleRows { get => (int)GetValue(VisibleRowsProperty); set => SetValue(VisibleRowsProperty, value); }

    /// <summary>Sets <see cref="WheelFormat"/> (same as the property setter).</summary>
    public SkUiDateTimePickerBase SetWheelFormat(string? value) { WheelFormat = value; return this; }

    /// <summary>Sets <see cref="IsLooping"/> (same as the property setter).</summary>
    public SkUiDateTimePickerBase SetIsLooping(bool value) { IsLooping = value; return this; }

    /// <summary>Sets <see cref="AreColumnHeadersVisible"/> (same as the property setter).</summary>
    public SkUiDateTimePickerBase SetAreColumnHeadersVisible(bool value) { AreColumnHeadersVisible = value; return this; }

    /// <summary>Sets <see cref="VisibleRows"/> (same as the property setter).</summary>
    public SkUiDateTimePickerBase SetVisibleRows(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiColumnsModel.MaximumVisibleRows);
        VisibleRows = value;
        return this;
    }

    /// <summary>A setting of the wheels changed: an open popup closes, the next one has it.</summary>
    private protected void ApplyWheels(Action<SkUiDateTimeModel> apply)
    {
        apply(Dates);
        Dates.WheelsChanged();
    }

    /// <summary>Checks an interval of the hour, minute or second wheel (1 up to <paramref name="count"/> - 1).</summary>
    internal static bool IsInterval(object? value, int count) => value is int interval && interval >= 1 && interval < count;

    /// <summary>Throws unless <paramref name="value"/> is an interval of a wheel of <paramref name="count"/> items.</summary>
    internal static void ThrowIfNotInterval(int value, int count, string name)
    {
        if (!IsInterval(value, count))
            throw new ArgumentOutOfRangeException(name, value, $"The interval must be from 1 to {count - 1}.");
    }
}
