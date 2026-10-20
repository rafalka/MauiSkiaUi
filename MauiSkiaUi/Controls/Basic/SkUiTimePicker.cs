using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A drawn time picker: MAUI's <c>TimePicker</c> API (<see cref="Time"/>, <see cref="Format"/>, <see cref="TimeSelected"/>,
/// <see cref="SkUiPickerBase.IsOpen"/> with <see cref="Opened"/> and <see cref="Closed"/>), so a XAML <c>TimePicker</c> becomes one by
/// changing the prefix; the field is drawn as the other pickers' and the popup holds wheels for the hour, the minute (and the
/// second), and AM / PM on a 12-hour clock (<see cref="SkUiDateTimePickerBase"/>), as the culture or
/// <see cref="SkUiDateTimePickerBase.WheelFormat"/> says. Beyond MAUI: a range
/// (<see cref="MinimumTime"/>, <see cref="MaximumTime"/>), steps (<see cref="HourInterval"/>, <see cref="MinuteInterval"/>,
/// <see cref="SecondInterval"/>), <see cref="DefaultHighlightedTime"/>, a clear button, a placeholder, OK / Cancel, and the app's own
/// time UI through the presenter (<see cref="SkUiTimePopupRequest"/>). Core twin: <see cref="Core.SkUiCoreTimePicker"/>.
/// </summary>
/// <remarks>
/// MAUI's rules: <see cref="Time"/> is midnight by default, a time of a day (from 0 to less than 24 hours; others are ignored),
/// <c>null</c> for none; <see cref="TimeSelected"/> is raised on every change. SkiaUi keeps it between <see cref="MinimumTime"/> and
/// <see cref="MaximumTime"/> as the date picker keeps its date. A time between the wheels' steps stays as the app set it; the wheels
/// show the step before it.
/// </remarks>
public class SkUiTimePicker : SkUiDateTimePickerBase
{
    private static readonly TimePickerOpenedEventArgs TimeOpenedArgs = new();
    private static readonly TimePickerClosedEventArgs TimeClosedArgs = new();

    /// <summary>Creates a picker of midnight.</summary>
    public SkUiTimePicker()
    {
        if (!IsSet(TimeProperty)) // a style's time is applied already
            Dates.Value = SkUiDateTimeWheels.TimeDate + TimeSpan.Zero;
        Dates.TextChanged();
    }

    private protected override SkUiDateTimeKind Kind => SkUiDateTimeKind.Time;

    /// <summary>Bindable property for <see cref="Time"/> (two-way by default).</summary>
    public static readonly BindableProperty TimeProperty = BindableProperty.Create(nameof(Time), typeof(TimeSpan?), typeof(SkUiTimePicker), TimeSpan.Zero, BindingMode.TwoWay,
        validateValue: (_, value) => value is not TimeSpan time || IsTimeOfDay(time),
        coerceValue: (picker, value) => ((SkUiTimePicker)picker).CoerceTime((TimeSpan?)value),
        propertyChanged: (picker, previous, value) => ((SkUiTimePicker)picker).OnTimeChanged((TimeSpan?)previous, (TimeSpan?)value));

    /// <summary>Bindable property for <see cref="MinimumTime"/>.</summary>
    public static readonly BindableProperty MinimumTimeProperty = BindableProperty.Create(nameof(MinimumTime), typeof(TimeSpan), typeof(SkUiTimePicker), TimeSpan.Zero,
        validateValue: (picker, value) => value is TimeSpan minimum && IsTimeOfDay(minimum) && minimum <= ((SkUiTimePicker)picker).MaximumTime,
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).OnRangeChanged(dates => dates.Minimum = SkUiDateTimeWheels.TimeDate + (TimeSpan)value));

    /// <summary>Bindable property for <see cref="MaximumTime"/>.</summary>
    public static readonly BindableProperty MaximumTimeProperty = BindableProperty.Create(nameof(MaximumTime), typeof(TimeSpan), typeof(SkUiTimePicker),
        SkUiDateTimeModel.LastTime,
        validateValue: (picker, value) => value is TimeSpan maximum && IsTimeOfDay(maximum) && maximum >= ((SkUiTimePicker)picker).MinimumTime,
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).OnRangeChanged(dates => dates.Maximum = SkUiDateTimeWheels.TimeDate + (TimeSpan)value));

    /// <summary>Bindable property for <see cref="Format"/>.</summary>
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(SkUiTimePicker), "t",
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).OnFormatChanged((string?)value));

    /// <summary>Bindable property for <see cref="DefaultHighlightedTime"/>.</summary>
    public static readonly BindableProperty DefaultHighlightedTimeProperty = BindableProperty.Create(nameof(DefaultHighlightedTime), typeof(TimeSpan?),
        typeof(SkUiTimePicker), null,
        validateValue: (_, value) => value is not TimeSpan time || IsTimeOfDay(time),
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).ApplyWheels(dates => dates.Highlighted = SkUiDateTimeWheels.TimeDate + (TimeSpan?)value));

    /// <summary>Bindable property for <see cref="HourInterval"/>.</summary>
    public static readonly BindableProperty HourIntervalProperty = BindableProperty.Create(nameof(HourInterval), typeof(int), typeof(SkUiTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 24),
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).ApplyWheels(dates => dates.HourInterval = (int)value));

    /// <summary>Bindable property for <see cref="MinuteInterval"/>.</summary>
    public static readonly BindableProperty MinuteIntervalProperty = BindableProperty.Create(nameof(MinuteInterval), typeof(int), typeof(SkUiTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 60),
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).ApplyWheels(dates => dates.MinuteInterval = (int)value));

    /// <summary>Bindable property for <see cref="SecondInterval"/>.</summary>
    public static readonly BindableProperty SecondIntervalProperty = BindableProperty.Create(nameof(SecondInterval), typeof(int), typeof(SkUiTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 60),
        propertyChanged: (picker, _, value) => ((SkUiTimePicker)picker).ApplyWheels(dates => dates.SecondInterval = (int)value));

    /// <summary>The chosen time of the day (0 to less than 24 hours); <c>null</c>: none. Midnight by default. Kept between <see cref="MinimumTime"/> and <see cref="MaximumTime"/>. Two-way by default.</summary>
    public TimeSpan? Time { get => (TimeSpan?)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <summary>The earliest time that can be chosen (default 0:00). SkiaUi extension.</summary>
    public TimeSpan MinimumTime { get => (TimeSpan)GetValue(MinimumTimeProperty); set => SetValue(MinimumTimeProperty, value); }

    /// <summary>The latest time that can be chosen (default 23:59:59). SkiaUi extension.</summary>
    public TimeSpan MaximumTime { get => (TimeSpan)GetValue(MaximumTimeProperty); set => SetValue(MaximumTimeProperty, value); }

    /// <summary>The field's .NET time format (default <c>"t"</c>, the culture's short time): <c>"T"</c>, <c>"HH:mm"</c>, <c>"h:mm tt"</c>, … The wheels follow <see cref="SkUiDateTimePickerBase.WheelFormat"/>.</summary>
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The time the wheels show while <see cref="Time"/> is <c>null</c>; <c>null</c> (default): now. On the wheels' steps, in the range. SkiaUi extension.</summary>
    public TimeSpan? DefaultHighlightedTime { get => (TimeSpan?)GetValue(DefaultHighlightedTimeProperty); set => SetValue(DefaultHighlightedTimeProperty, value); }

    /// <summary>The hour wheel's step (default 1; 1–23). SkiaUi extension.</summary>
    public int HourInterval { get => (int)GetValue(HourIntervalProperty); set => SetValue(HourIntervalProperty, value); }

    /// <summary>The minute wheel's step (default 1; 1–59): 5 shows 00, 05, 10, …. SkiaUi extension.</summary>
    public int MinuteInterval { get => (int)GetValue(MinuteIntervalProperty); set => SetValue(MinuteIntervalProperty, value); }

    /// <summary>The second wheel's step (default 1; 1–59), when the wheel format has seconds. SkiaUi extension.</summary>
    public int SecondInterval { get => (int)GetValue(SecondIntervalProperty); set => SetValue(SecondIntervalProperty, value); }

    /// <summary>Raised when <see cref="Time"/> changes (the user, or the app), with the times before and after.</summary>
    public event EventHandler<TimeChangedEventArgs>? TimeSelected;

    /// <summary>Raised when <see cref="SkUiPickerBase.IsOpen"/> becomes <c>true</c> (MAUI's event type; <see cref="SkUiPickerBase.Opened"/> is raised too).</summary>
    public new event EventHandler<TimePickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="SkUiPickerBase.IsOpen"/> becomes <c>false</c> (MAUI's event type; <see cref="SkUiPickerBase.Closed"/> is raised too).</summary>
    public new event EventHandler<TimePickerClosedEventArgs>? Closed;

    /// <summary>Sets <see cref="Time"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day.</exception>
    public SkUiTimePicker SetTime(TimeSpan? value)
    {
        if (value is { } time && !IsTimeOfDay(time))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time must be from 0 to less than 24 hours.");
        Time = value;
        return this;
    }

    /// <summary>Sets <see cref="MinimumTime"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day, or after <see cref="MaximumTime"/>.</exception>
    public SkUiTimePicker SetMinimumTime(TimeSpan value)
    {
        if (!IsTimeOfDay(value) || value > MaximumTime)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum time must be a time of a day, not after the maximum time.");
        MinimumTime = value;
        return this;
    }

    /// <summary>Sets <see cref="MaximumTime"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day, or before <see cref="MinimumTime"/>.</exception>
    public SkUiTimePicker SetMaximumTime(TimeSpan value)
    {
        if (!IsTimeOfDay(value) || value < MinimumTime)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum time must be a time of a day, not before the minimum time.");
        MaximumTime = value;
        return this;
    }

    /// <summary>Sets <see cref="Format"/> (same as the property setter).</summary>
    public SkUiTimePicker SetFormat(string value) { ArgumentNullException.ThrowIfNull(value); Format = value; return this; }

    /// <summary>Sets <see cref="DefaultHighlightedTime"/> (same as the property setter).</summary>
    public SkUiTimePicker SetDefaultHighlightedTime(TimeSpan? value)
    {
        if (value is { } time && !IsTimeOfDay(time))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time must be from 0 to less than 24 hours.");
        DefaultHighlightedTime = value;
        return this;
    }

    /// <summary>Sets <see cref="HourInterval"/> (same as the property setter).</summary>
    public SkUiTimePicker SetHourInterval(int value) { ThrowIfNotInterval(value, 24, nameof(value)); HourInterval = value; return this; }

    /// <summary>Sets <see cref="MinuteInterval"/> (same as the property setter).</summary>
    public SkUiTimePicker SetMinuteInterval(int value) { ThrowIfNotInterval(value, 60, nameof(value)); MinuteInterval = value; return this; }

    /// <summary>Sets <see cref="SecondInterval"/> (same as the property setter).</summary>
    public SkUiTimePicker SetSecondInterval(int value) { ThrowIfNotInterval(value, 60, nameof(value)); SecondInterval = value; return this; }

    /// <summary>Whether <paramref name="time"/> is a time of a day (MAUI's rule: from 0 to less than 24 hours).</summary>
    internal static bool IsTimeOfDay(TimeSpan time) => time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);

    private TimeSpan? CoerceTime(TimeSpan? value)
    {
        if (value > MaximumTime)
            value = MaximumTime;
        if (value < MinimumTime)
            value = MinimumTime;
        return value;
    }

    private void OnRangeChanged(Action<SkUiDateTimeModel> apply)
    {
        ApplyWheels(apply);
        // MAUI's: a value outside the new range moves into it (BindableObject.CoerceValue does not store the coerced value).
        if (Time is { } value && CoerceTime(value) is var coerced && coerced != value)
            Time = coerced;
    }

    private void OnFormatChanged(string? value)
    {
        Dates.Format = value ?? SkUiDateTimeModel.DefaultFormat(SkUiDateTimeKind.Time);
        Dates.TextChanged();
    }

    private void OnTimeChanged(TimeSpan? previous, TimeSpan? value)
    {
        Dates.Value = SkUiDateTimeWheels.TimeDate + value;
        Dates.ValueChanged();
        Model.RunSelectionChangedCommand();
        TimeSelected?.Invoke(this, new TimeChangedEventArgs(previous, value));
    }

    /// <inheritdoc />
    private protected override void ChooseValue(DateTime? value) => Time = value?.TimeOfDay;

    /// <inheritdoc />
    private protected override void ClearChoice() => Time = null;

    /// <inheritdoc />
    private protected override void RaiseOpenedOrClosed(bool opened)
    {
        base.RaiseOpenedOrClosed(opened);
        if (opened)
            Opened?.Invoke(this, TimeOpenedArgs);
        else
            Closed?.Invoke(this, TimeClosedArgs);
    }

    /// <summary>Sets <see cref="SkUiPickerBase.Title"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TitleColor"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TextColor"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontFamily"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontSize"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAttributes"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CharacterSpacing"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.HorizontalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.VerticalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsOpen"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public new SkUiTimePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPlacement"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupBounds"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPresenter"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetFieldStyle" />
    public new SkUiTimePicker SetFieldStyle(SkUiPickerFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetFillColor" />
    public new SkUiTimePicker SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetBorderColor" />
    public new SkUiTimePicker SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetBorderWidth" />
    public new SkUiTimePicker SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetCornerRadii" />
    public new SkUiTimePicker SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetCornerRadius" />
    public new SkUiTimePicker SetCornerRadius(int? value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.WheelFormat"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetWheelFormat(string? value) { base.SetWheelFormat(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.IsLooping"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetIsLooping(bool value) { base.SetIsLooping(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.AreColumnHeadersVisible"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetAreColumnHeadersVisible(bool value) { base.SetAreColumnHeadersVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.VisibleRows"/> (same as the property setter).</summary>
    public new SkUiTimePicker SetVisibleRows(int value) { base.SetVisibleRows(value); return this; }
}
