using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A drawn picker of a day and a time of it, on one popup of wheels (day, month, year, hour, minute, AM / PM; ordered and named by
/// the culture, or by <see cref="SkUiDateTimePickerBase.WheelFormat"/>): an appointment, a reminder, a departure. MAUI has none.
/// The API is
/// <see cref="SkUiDatePicker"/>'s with the time kept (<see cref="Date"/>, <see cref="MinimumDate"/> and <see cref="MaximumDate"/>
/// include it), plus the time picker's steps (<see cref="HourInterval"/>, <see cref="MinuteInterval"/>,
/// <see cref="SecondInterval"/>). Core twin: <see cref="Core.SkUiCoreDateTimePicker"/>.
/// </summary>
/// <remarks>
/// <see cref="Date"/> is <c>null</c> by default (the field shows the placeholder or the title; the wheels show
/// <see cref="DefaultHighlightedDate"/>, else now) and is kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>
/// (moving them moves it). The minimum and maximum apply to the time too: on the first day only the times from the minimum's
/// can be chosen.
/// </remarks>
public class SkUiDateTimePicker : SkUiDateTimePickerBase
{
    /// <summary>The latest moment by default: the end of 31 December 2100.</summary>
    private static readonly DateTime DefaultMaximum = SkUiDateTimeModel.DefaultMaximumDate + SkUiDateTimeModel.LastTime;

    /// <summary>Creates a picker without a date and time.</summary>
    public SkUiDateTimePicker() : base(SkUiDateTimeKind.DateAndTime) => Dates.TextChanged();

    /// <summary>Bindable property for <see cref="Date"/> (two-way by default).</summary>
    public static readonly BindableProperty DateProperty = BindableProperty.Create(nameof(Date), typeof(DateTime?), typeof(SkUiDateTimePicker), null, BindingMode.TwoWay,
        coerceValue: (picker, value) => ((SkUiDateTimePicker)picker).CoerceDate((DateTime?)value),
        propertyChanged: (picker, previous, value) => ((SkUiDateTimePicker)picker).OnDateChanged((DateTime?)previous, (DateTime?)value));

    /// <summary>Bindable property for <see cref="MinimumDate"/>.</summary>
    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime?), typeof(SkUiDateTimePicker),
        SkUiDateTimeModel.DefaultMinimumDate,
        validateValue: (picker, value) => value is not DateTime minimum || ((SkUiDateTimePicker)picker).MaximumDate is not { } maximum || minimum <= maximum,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).OnRangeChanged(dates => dates.Minimum = (DateTime?)value ?? DateTime.MinValue));

    /// <summary>Bindable property for <see cref="MaximumDate"/>.</summary>
    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime?), typeof(SkUiDateTimePicker),
        DefaultMaximum,
        validateValue: (picker, value) => value is not DateTime maximum || ((SkUiDateTimePicker)picker).MinimumDate is not { } minimum || maximum >= minimum,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).OnRangeChanged(dates => dates.Maximum = (DateTime?)value ?? DateTime.MaxValue));

    /// <summary>Bindable property for <see cref="Format"/>.</summary>
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(SkUiDateTimePicker), "g",
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).OnFormatChanged((string?)value));

    /// <summary>Bindable property for <see cref="DefaultHighlightedDate"/>.</summary>
    public static readonly BindableProperty DefaultHighlightedDateProperty = BindableProperty.Create(nameof(DefaultHighlightedDate), typeof(DateTime?),
        typeof(SkUiDateTimePicker), null,
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).ApplyWheels(dates => dates.Highlighted = (DateTime?)value));

    /// <summary>Bindable property for <see cref="BlackoutDates"/>.</summary>
    public static readonly BindableProperty BlackoutDatesProperty = BindableProperty.Create(nameof(BlackoutDates), typeof(IList<DateTime>), typeof(SkUiDateTimePicker),
        null, propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).ApplyWheels(dates => dates.BlackoutDates = (IList<DateTime>?)value),
        defaultValueCreator: picker => ((SkUiDateTimePicker)picker).CreateBlackoutDates());

    /// <summary>Bindable property for <see cref="HourInterval"/>.</summary>
    public static readonly BindableProperty HourIntervalProperty = BindableProperty.Create(nameof(HourInterval), typeof(int), typeof(SkUiDateTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 24),
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).ApplyWheels(dates => dates.HourInterval = (int)value));

    /// <summary>Bindable property for <see cref="MinuteInterval"/>.</summary>
    public static readonly BindableProperty MinuteIntervalProperty = BindableProperty.Create(nameof(MinuteInterval), typeof(int), typeof(SkUiDateTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 60),
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).ApplyWheels(dates => dates.MinuteInterval = (int)value));

    /// <summary>Bindable property for <see cref="SecondInterval"/>.</summary>
    public static readonly BindableProperty SecondIntervalProperty = BindableProperty.Create(nameof(SecondInterval), typeof(int), typeof(SkUiDateTimePicker), 1,
        validateValue: (_, value) => IsInterval(value, 60),
        propertyChanged: (picker, _, value) => ((SkUiDateTimePicker)picker).ApplyWheels(dates => dates.SecondInterval = (int)value));

    /// <summary>The chosen day and time; <c>null</c> (default): none. Kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>. Two-way by default.</summary>
    public DateTime? Date { get => (DateTime?)GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <summary>The earliest moment that can be chosen (default the start of 1 January 1900); <c>null</c>: none.</summary>
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }

    /// <summary>The latest moment that can be chosen (default the end of 31 December 2100); <c>null</c>: none.</summary>
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    /// <summary>The field's .NET date and time format (default <c>"g"</c>, the culture's short date and time).</summary>
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>The moment the wheels show while <see cref="Date"/> is <c>null</c>; <c>null</c> (default): now. On the wheels' steps, in the range. SkiaUi extension.</summary>
    public DateTime? DefaultHighlightedDate { get => (DateTime?)GetValue(DefaultHighlightedDateProperty); set => SetValue(DefaultHighlightedDateProperty, value); }

    /// <summary>Days that cannot be chosen (an empty list by default; read when the popup opens). SkiaUi extension.</summary>
    public IList<DateTime>? BlackoutDates { get => (IList<DateTime>?)GetValue(BlackoutDatesProperty); set => SetValue(BlackoutDatesProperty, value); }

    /// <summary>Whether a day can be chosen, a rule of the app's; <c>null</c> (default): every day in the range. Not bindable. SkiaUi extension.</summary>
    public Func<DateTime, bool>? DateEnabled
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            OnPropertyChanging();
            field = value;
            ApplyWheels(dates => dates.DateEnabled = value);
            OnPropertyChanged();
        }
    }

    /// <summary>The hour wheel's step (default 1; 1–23): 2 shows 0, 2, 4, …. SkiaUi extension.</summary>
    public int HourInterval { get => (int)GetValue(HourIntervalProperty); set => SetValue(HourIntervalProperty, value); }

    /// <summary>The minute wheel's step (default 1; 1–59): 15 shows quarter hours. SkiaUi extension.</summary>
    public int MinuteInterval { get => (int)GetValue(MinuteIntervalProperty); set => SetValue(MinuteIntervalProperty, value); }

    /// <summary>The second wheel's step (default 1; 1–59). SkiaUi extension.</summary>
    public int SecondInterval { get => (int)GetValue(SecondIntervalProperty); set => SetValue(SecondIntervalProperty, value); }

    /// <summary>Raised when <see cref="Date"/> changes (the user, or the app), with the values before and after.</summary>
    public event EventHandler<DateChangedEventArgs>? DateSelected;

    /// <summary>Sets <see cref="Date"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetDate(DateTime? value) { Date = value; return this; }

    /// <summary>Sets <see cref="MinimumDate"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is after <see cref="MaximumDate"/>.</exception>
    public SkUiDateTimePicker SetMinimumDate(DateTime? value)
    {
        if (value is { } minimum && MaximumDate is { } maximum && minimum > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum date must not be after the maximum date.");
        MinimumDate = value;
        return this;
    }

    /// <summary>Sets <see cref="MaximumDate"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is before <see cref="MinimumDate"/>.</exception>
    public SkUiDateTimePicker SetMaximumDate(DateTime? value)
    {
        if (value is { } maximum && MinimumDate is { } minimum && maximum < minimum)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum date must not be before the minimum date.");
        MaximumDate = value;
        return this;
    }

    /// <summary>Sets <see cref="Format"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetFormat(string value) { ArgumentNullException.ThrowIfNull(value); Format = value; return this; }

    /// <summary>Sets <see cref="DefaultHighlightedDate"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetDefaultHighlightedDate(DateTime? value) { DefaultHighlightedDate = value; return this; }

    /// <summary>Sets <see cref="BlackoutDates"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetBlackoutDates(IList<DateTime>? value) { BlackoutDates = value; return this; }

    /// <summary>Sets <see cref="DateEnabled"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetDateEnabled(Func<DateTime, bool>? value) { DateEnabled = value; return this; }

    /// <summary>Sets <see cref="HourInterval"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetHourInterval(int value) { ThrowIfNotInterval(value, 24, nameof(value)); HourInterval = value; return this; }

    /// <summary>Sets <see cref="MinuteInterval"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetMinuteInterval(int value) { ThrowIfNotInterval(value, 60, nameof(value)); MinuteInterval = value; return this; }

    /// <summary>Sets <see cref="SecondInterval"/> (same as the property setter).</summary>
    public SkUiDateTimePicker SetSecondInterval(int value) { ThrowIfNotInterval(value, 60, nameof(value)); SecondInterval = value; return this; }

    private IList<DateTime> CreateBlackoutDates()
    {
        var dates = new ObservableCollection<DateTime>();
        dates.CollectionChanged += (_, _) => Dates.WheelsChanged();
        Dates.BlackoutDates = dates;
        return dates;
    }

    private DateTime? CoerceDate(DateTime? value)
    {
        if (value > MaximumDate)
            value = MaximumDate;
        if (value < MinimumDate)
            value = MinimumDate;
        return value;
    }

    private void OnRangeChanged(Action<SkUiDateTimeModel> apply)
    {
        ApplyWheels(apply);
        // MAUI's: a value outside the new range moves into it (BindableObject.CoerceValue does not store the coerced value).
        if (Date is { } value && CoerceDate(value) is var coerced && coerced != value)
            Date = coerced;
    }

    private void OnFormatChanged(string? value)
    {
        Dates.Format = value ?? SkUiDateTimeModel.DefaultFormat(SkUiDateTimeKind.DateAndTime);
        Dates.TextChanged();
    }

    private void OnDateChanged(DateTime? previous, DateTime? value)
    {
        Dates.Value = value;
        Dates.ValueChanged();
        Model.RunSelectionChangedCommand();
        DateSelected?.Invoke(this, new DateChangedEventArgs(previous, value));
    }

    /// <inheritdoc />
    private protected override void ChooseValue(DateTime? value) => Date = value;

    /// <inheritdoc />
    private protected override void ClearChoice() => Date = null;

    /// <summary>Sets <see cref="SkUiPickerBase.Title"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TitleColor"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TextColor"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontFamily"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontSize"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAttributes"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CharacterSpacing"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.HorizontalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.VerticalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsOpen"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public new SkUiDateTimePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPlacement"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupBounds"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPresenter"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.WheelFormat"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetWheelFormat(string? value) { base.SetWheelFormat(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.IsLooping"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetIsLooping(bool value) { base.SetIsLooping(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.AreColumnHeadersVisible"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetAreColumnHeadersVisible(bool value) { base.SetAreColumnHeadersVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.VisibleRows"/> (same as the property setter).</summary>
    public new SkUiDateTimePicker SetVisibleRows(int value) { base.SetVisibleRows(value); return this; }
}
