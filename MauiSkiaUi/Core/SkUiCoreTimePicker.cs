using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn time picker (Core analogue of <see cref="SkUiTimePicker"/>): the same field, wheels and rules (MAUI's time, a range, the
/// wheels' steps, the highlighted time, the wheel format), set with fluent <c>Set*</c> calls.
/// </summary>
/// <remarks>
/// While disabled (<see cref="SkUiCoreNode.IsEnabled"/> <c>false</c>, also through a Core ancestor or the host) the picker draws
/// its disabled look and does not open; an open popup closes.
/// </remarks>
public class SkUiCoreTimePicker : SkUiCoreDateTimePickerBase
{
    private TimeSpan? _time = TimeSpan.Zero;
    private TimeSpan _minimumTime = TimeSpan.Zero;
    private TimeSpan _maximumTime = SkUiDateTimeModel.LastTime;
    private TimeSpan? _defaultHighlightedTime;

    /// <summary>Creates a picker of midnight.</summary>
    public SkUiCoreTimePicker() : base(SkUiDateTimeKind.Time)
    {
        Dates.Value = SkUiDateTimeWheels.TimeDate + _time;
        Dates.TextChanged();
    }

    /// <summary>The chosen time of the day (0 to less than 24 hours); <c>null</c>: none. Midnight by default. Kept between <see cref="MinimumTime"/> and <see cref="MaximumTime"/>.</summary>
    public TimeSpan? Time { get => _time; set => SetTime(value); }

    /// <summary>The earliest time that can be chosen (default 0:00).</summary>
    public TimeSpan MinimumTime { get => _minimumTime; set => SetMinimumTime(value); }

    /// <summary>The latest time that can be chosen (default 23:59:59).</summary>
    public TimeSpan MaximumTime { get => _maximumTime; set => SetMaximumTime(value); }

    /// <summary>The field's .NET time format (default <c>"t"</c>).</summary>
    public string Format { get => Dates.Format; set => SetFormat(value); }

    /// <summary>The time the wheels show while <see cref="Time"/> is <c>null</c>; <c>null</c> (default): now.</summary>
    public TimeSpan? DefaultHighlightedTime { get => _defaultHighlightedTime; set => SetDefaultHighlightedTime(value); }

    /// <summary>The hour wheel's step (default 1; 1–23).</summary>
    public int HourInterval { get => Dates.HourInterval; set => SetHourInterval(value); }

    /// <summary>The minute wheel's step (default 1; 1–59).</summary>
    public int MinuteInterval { get => Dates.MinuteInterval; set => SetMinuteInterval(value); }

    /// <summary>The second wheel's step (default 1; 1–59).</summary>
    public int SecondInterval { get => Dates.SecondInterval; set => SetSecondInterval(value); }

    /// <summary>Raised when <see cref="Time"/> changes (the user, or the app), with the times before and after.</summary>
    public event EventHandler<TimeChangedEventArgs>? TimeSelected;

    /// <summary>Sets <see cref="Time"/> (kept in the range).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day.</exception>
    public SkUiCoreTimePicker SetTime(TimeSpan? value)
    {
        if (value is { } time && !SkUiTimePicker.IsTimeOfDay(time))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time must be from 0 to less than 24 hours.");
        if (value > _maximumTime)
            value = _maximumTime;
        if (value < _minimumTime)
            value = _minimumTime;
        var previous = _time;
        if (!SetProperty(ref _time, value, nameof(Time)))
            return this;
        Dates.Value = SkUiDateTimeWheels.TimeDate + value;
        Dates.ValueChanged();
        Model.RunSelectionChangedCommand();
        TimeSelected?.Invoke(this, new TimeChangedEventArgs(previous, value));
        return this;
    }

    /// <summary>Sets <see cref="MinimumTime"/>; a time before it moves to it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day, or after <see cref="MaximumTime"/>.</exception>
    public SkUiCoreTimePicker SetMinimumTime(TimeSpan value)
    {
        if (!SkUiTimePicker.IsTimeOfDay(value) || value > _maximumTime)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum time must be a time of a day, not after the maximum time.");
        if (!SetProperty(ref _minimumTime, value, nameof(MinimumTime)))
            return this;
        Dates.Minimum = SkUiDateTimeWheels.TimeDate + value;
        Wheels(true);
        return _time is { } time ? SetTime(time) : this;
    }

    /// <summary>Sets <see cref="MaximumTime"/>; a time after it moves to it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day, or before <see cref="MinimumTime"/>.</exception>
    public SkUiCoreTimePicker SetMaximumTime(TimeSpan value)
    {
        if (!SkUiTimePicker.IsTimeOfDay(value) || value < _minimumTime)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum time must be a time of a day, not before the minimum time.");
        if (!SetProperty(ref _maximumTime, value, nameof(MaximumTime)))
            return this;
        Dates.Maximum = SkUiDateTimeWheels.TimeDate + value;
        Wheels(true);
        return _time is { } time ? SetTime(time) : this;
    }

    /// <summary>Sets <see cref="Format"/>.</summary>
    public SkUiCoreTimePicker SetFormat(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref Dates.Format, value, nameof(Format)))
            Dates.TextChanged();
        return this;
    }

    /// <summary>Sets <see cref="DefaultHighlightedTime"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a time of a day.</exception>
    public SkUiCoreTimePicker SetDefaultHighlightedTime(TimeSpan? value)
    {
        if (value is { } time && !SkUiTimePicker.IsTimeOfDay(time))
            throw new ArgumentOutOfRangeException(nameof(value), value, "The time must be from 0 to less than 24 hours.");
        if (SetProperty(ref _defaultHighlightedTime, value, nameof(DefaultHighlightedTime)))
        {
            Dates.Highlighted = SkUiDateTimeWheels.TimeDate + value;
            Wheels(true);
        }
        return this;
    }

    /// <summary>Sets <see cref="HourInterval"/> (1–23).</summary>
    public SkUiCoreTimePicker SetHourInterval(int value)
    {
        SkUiDateTimePickerBase.ThrowIfNotInterval(value, 24, nameof(value));
        Wheels(SetProperty(ref Dates.HourInterval, value, nameof(HourInterval)));
        return this;
    }

    /// <summary>Sets <see cref="MinuteInterval"/> (1–59).</summary>
    public SkUiCoreTimePicker SetMinuteInterval(int value)
    {
        SkUiDateTimePickerBase.ThrowIfNotInterval(value, 60, nameof(value));
        Wheels(SetProperty(ref Dates.MinuteInterval, value, nameof(MinuteInterval)));
        return this;
    }

    /// <summary>Sets <see cref="SecondInterval"/> (1–59).</summary>
    public SkUiCoreTimePicker SetSecondInterval(int value)
    {
        SkUiDateTimePickerBase.ThrowIfNotInterval(value, 60, nameof(value));
        Wheels(SetProperty(ref Dates.SecondInterval, value, nameof(SecondInterval)));
        return this;
    }

    /// <inheritdoc />
    private protected override void ChooseValue(DateTime? value) => SetTime(value?.TimeOfDay);

    /// <inheritdoc />
    private protected override void ClearChoice() => SetTime(null);

    /// <summary>Sets <see cref="SkUiCorePickerBase.Title"/>.</summary>
    public new SkUiCoreTimePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TitleColor"/>.</summary>
    public new SkUiCoreTimePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TextColor"/>.</summary>
    public new SkUiCoreTimePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontFamily"/>.</summary>
    public new SkUiCoreTimePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontSize"/>.</summary>
    public new SkUiCoreTimePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAttributes"/>.</summary>
    public new SkUiCoreTimePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCoreTimePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CharacterSpacing"/>.</summary>
    public new SkUiCoreTimePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.HorizontalTextAlignment"/>.</summary>
    public new SkUiCoreTimePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.VerticalTextAlignment"/>.</summary>
    public new SkUiCoreTimePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsOpen"/> (opens or closes the list).</summary>
    public new SkUiCoreTimePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupHeight"/> (-1: no limit).</summary>
    public new SkUiCoreTimePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPlacement"/>.</summary>
    public new SkUiCoreTimePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupBounds"/>.</summary>
    public new SkUiCoreTimePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPresenter"/>.</summary>
    public new SkUiCoreTimePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFieldStyle" />
    public new SkUiCoreTimePicker SetFieldStyle(SkUiPickerFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFillColor" />
    public new SkUiCoreTimePicker SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderColor" />
    public new SkUiCoreTimePicker SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderWidth" />
    public new SkUiCoreTimePicker SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadii" />
    public new SkUiCoreTimePicker SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadius" />
    public new SkUiCoreTimePicker SetCornerRadius(double? value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiCoreTimePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.WheelFormat"/>.</summary>
    public new SkUiCoreTimePicker SetWheelFormat(string? value) { base.SetWheelFormat(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.IsLooping"/>.</summary>
    public new SkUiCoreTimePicker SetIsLooping(bool value) { base.SetIsLooping(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.AreColumnHeadersVisible"/>.</summary>
    public new SkUiCoreTimePicker SetAreColumnHeadersVisible(bool value) { base.SetAreColumnHeadersVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.VisibleRows"/> (1–9).</summary>
    public new SkUiCoreTimePicker SetVisibleRows(int value) { base.SetVisibleRows(value); return this; }
}
