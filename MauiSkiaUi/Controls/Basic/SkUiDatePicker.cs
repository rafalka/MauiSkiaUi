using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>
/// A drawn date picker: MAUI's <c>DatePicker</c> API (<see cref="Date"/>, <see cref="MinimumDate"/>, <see cref="MaximumDate"/>,
/// <see cref="Format"/>, <see cref="DateSelected"/>, <see cref="SkUiPickerBase.IsOpen"/> with <see cref="Opened"/> and
/// <see cref="Closed"/>), so a XAML <c>DatePicker</c> becomes one by changing the prefix; the field is drawn as the other pickers'
/// and the popup holds wheels for the day, the month and the year (<see cref="SkUiDateTimePickerBase"/>) on the app's popup system.
/// Beyond MAUI: <see cref="BlackoutDates"/> and <see cref="DateEnabled"/> (days that cannot be chosen),
/// <see cref="DefaultHighlightedDate"/>, a month and year picker by <see cref="SkUiDateTimePickerBase.WheelFormat"/>, a clear button,
/// a placeholder, OK / Cancel, the app's own date UI through the presenter (<see cref="SkUiDatePopupRequest"/>). Core twin:
/// <see cref="Core.SkUiCoreDatePicker"/>; a day and its time: <see cref="SkUiDateTimePicker"/>.
/// </summary>
/// <remarks>
/// MAUI's rules: <see cref="Date"/> is today by default, a date without its time, kept between <see cref="MinimumDate"/> and
/// <see cref="MaximumDate"/> (moving them moves it), and <c>null</c> for none (the field shows the placeholder or the title);
/// <see cref="DateSelected"/> is raised on every change.
/// </remarks>
public class SkUiDatePicker : SkUiDateTimePickerBase
{
    private static readonly DatePickerOpenedEventArgs DateOpenedArgs = new();
    private static readonly DatePickerClosedEventArgs DateClosedArgs = new();

    /// <summary>Creates a picker of today's date.</summary>
    public SkUiDatePicker()
    {
        Dates.Value = Date;
        Dates.TextChanged();
    }

    private protected override SkUiDateTimeKind Kind => SkUiDateTimeKind.Date;

    /// <summary>Bindable property for <see cref="Date"/> (two-way by default).</summary>
    public static readonly BindableProperty DateProperty = BindableProperty.Create(nameof(Date), typeof(DateTime?), typeof(SkUiDatePicker), null, BindingMode.TwoWay,
        coerceValue: (picker, value) => ((SkUiDatePicker)picker).CoerceDate((DateTime?)value),
        propertyChanged: (picker, previous, value) => ((SkUiDatePicker)picker).OnDateChanged((DateTime?)previous, (DateTime?)value),
        defaultValueCreator: _ => DateTime.Today);

    /// <summary>Bindable property for <see cref="MinimumDate"/>.</summary>
    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime?), typeof(SkUiDatePicker),
        SkUiDateTimeModel.DefaultMinimumDate,
        validateValue: (picker, value) => value is not DateTime minimum || ((SkUiDatePicker)picker).MaximumDate is not { } maximum || minimum.Date <= maximum.Date,
        coerceValue: (_, value) => ((DateTime?)value)?.Date,
        propertyChanged: (picker, _, value) => ((SkUiDatePicker)picker).OnRangeChanged(dates => dates.Minimum = ((DateTime?)value)?.Date ?? DateTime.MinValue));

    /// <summary>Bindable property for <see cref="MaximumDate"/>.</summary>
    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime?), typeof(SkUiDatePicker),
        SkUiDateTimeModel.DefaultMaximumDate,
        validateValue: (picker, value) => value is not DateTime maximum || ((SkUiDatePicker)picker).MinimumDate is not { } minimum || maximum.Date >= minimum.Date,
        coerceValue: (_, value) => ((DateTime?)value)?.Date,
        propertyChanged: (picker, _, value) => ((SkUiDatePicker)picker).OnRangeChanged(dates => dates.Maximum = ((DateTime?)value)?.Date ?? DateTime.MaxValue.Date));

    /// <summary>Bindable property for <see cref="Format"/>.</summary>
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(SkUiDatePicker), "d",
        propertyChanged: (picker, _, value) => ((SkUiDatePicker)picker).OnFormatChanged((string?)value));

    /// <summary>Bindable property for <see cref="DefaultHighlightedDate"/>.</summary>
    public static readonly BindableProperty DefaultHighlightedDateProperty = BindableProperty.Create(nameof(DefaultHighlightedDate), typeof(DateTime?),
        typeof(SkUiDatePicker), null,
        propertyChanged: (picker, _, value) => ((SkUiDatePicker)picker).ApplyWheels(dates => dates.Highlighted = ((DateTime?)value)?.Date));

    /// <summary>Bindable property for <see cref="BlackoutDates"/>.</summary>
    public static readonly BindableProperty BlackoutDatesProperty = BindableProperty.Create(nameof(BlackoutDates), typeof(IList<DateTime>), typeof(SkUiDatePicker), null,
        propertyChanged: (picker, _, value) => ((SkUiDatePicker)picker).ApplyWheels(dates => dates.BlackoutDates = (IList<DateTime>?)value),
        defaultValueCreator: picker => ((SkUiDatePicker)picker).CreateBlackoutDates());

    /// <summary>The chosen date (its time is midnight); <c>null</c>: none. Today by default. Kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>. Two-way by default.</summary>
    public DateTime? Date { get => (DateTime?)GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <summary>The earliest date that can be chosen (default 1 January 1900); <c>null</c>: none (the year wheel then shows 200 years either side of the date).</summary>
    public DateTime? MinimumDate { get => (DateTime?)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }

    /// <summary>The latest date that can be chosen (default 31 December 2100); <c>null</c>: none.</summary>
    public DateTime? MaximumDate { get => (DateTime?)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }

    /// <summary>The field's .NET date format (default <c>"d"</c>, the culture's short date): <c>"D"</c>, <c>"dd MMM yyyy"</c>, … The wheels follow <see cref="SkUiDateTimePickerBase.WheelFormat"/>.</summary>
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>
    /// The date the wheels show while <see cref="Date"/> is <c>null</c> (a date of birth's wheels on 1990); <c>null</c> (default):
    /// today. Kept in the range. SkiaUi extension.
    /// </summary>
    public DateTime? DefaultHighlightedDate { get => (DateTime?)GetValue(DefaultHighlightedDateProperty); set => SetValue(DefaultHighlightedDateProperty, value); }

    /// <summary>
    /// Days that cannot be chosen (holidays, booked days): their day items are dimmed and passed by, as days outside the range.
    /// An empty list by default (add to it, or set your own); read when the popup opens. The app may still set such a
    /// <see cref="Date"/>. SkiaUi extension.
    /// </summary>
    public IList<DateTime>? BlackoutDates { get => (IList<DateTime>?)GetValue(BlackoutDatesProperty); set => SetValue(BlackoutDatesProperty, value); }

    /// <summary>
    /// Whether a day can be chosen, a rule of the app's (weekdays only: <c>date =&gt; date.DayOfWeek is not (DayOfWeek.Saturday
    /// or DayOfWeek.Sunday)</c>); <c>null</c> (default): every day in the range. Not bindable; asked while the popup is open.
    /// SkiaUi extension.
    /// </summary>
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

    /// <summary>Raised when <see cref="Date"/> changes (the user, or the app), with the dates before and after.</summary>
    public event EventHandler<DateChangedEventArgs>? DateSelected;

    /// <summary>Raised when <see cref="SkUiPickerBase.IsOpen"/> becomes <c>true</c> (MAUI's event type; <see cref="SkUiPickerBase.Opened"/> is raised too).</summary>
    public new event EventHandler<DatePickerOpenedEventArgs>? Opened;

    /// <summary>Raised when <see cref="SkUiPickerBase.IsOpen"/> becomes <c>false</c> (MAUI's event type; <see cref="SkUiPickerBase.Closed"/> is raised too).</summary>
    public new event EventHandler<DatePickerClosedEventArgs>? Closed;

    /// <summary>Sets <see cref="Date"/> (same as the property setter).</summary>
    public SkUiDatePicker SetDate(DateTime? value) { Date = value; return this; }

    /// <summary>Sets <see cref="MinimumDate"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is after <see cref="MaximumDate"/>.</exception>
    public SkUiDatePicker SetMinimumDate(DateTime? value)
    {
        if (value is { } minimum && MaximumDate is { } maximum && minimum.Date > maximum.Date)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum date must not be after the maximum date.");
        MinimumDate = value;
        return this;
    }

    /// <summary>Sets <see cref="MaximumDate"/> (same as the property setter).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is before <see cref="MinimumDate"/>.</exception>
    public SkUiDatePicker SetMaximumDate(DateTime? value)
    {
        if (value is { } maximum && MinimumDate is { } minimum && maximum.Date < minimum.Date)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum date must not be before the minimum date.");
        MaximumDate = value;
        return this;
    }

    /// <summary>Sets <see cref="Format"/> (same as the property setter).</summary>
    public SkUiDatePicker SetFormat(string value) { ArgumentNullException.ThrowIfNull(value); Format = value; return this; }

    /// <summary>Sets <see cref="DefaultHighlightedDate"/> (same as the property setter).</summary>
    public SkUiDatePicker SetDefaultHighlightedDate(DateTime? value) { DefaultHighlightedDate = value; return this; }

    /// <summary>Sets <see cref="BlackoutDates"/> (same as the property setter).</summary>
    public SkUiDatePicker SetBlackoutDates(IList<DateTime>? value) { BlackoutDates = value; return this; }

    /// <summary>Sets <see cref="DateEnabled"/> (same as the property setter).</summary>
    public SkUiDatePicker SetDateEnabled(Func<DateTime, bool>? value) { DateEnabled = value; return this; }

    private IList<DateTime> CreateBlackoutDates()
    {
        var dates = new ObservableCollection<DateTime>();
        dates.CollectionChanged += (_, _) => Dates.WheelsChanged();
        Dates.BlackoutDates = dates;
        return dates;
    }

    private DateTime? CoerceDate(DateTime? value)
    {
        var date = value?.Date;
        if (date > MaximumDate)
            date = MaximumDate;
        if (date < MinimumDate)
            date = MinimumDate;
        return date;
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
        Dates.Format = value ?? SkUiDateTimeModel.DefaultFormat(SkUiDateTimeKind.Date);
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
    private protected override void ChooseValue(DateTime? value) => Date = value?.Date;

    /// <inheritdoc />
    private protected override void ClearChoice() => Date = null;

    /// <inheritdoc />
    private protected override void RaiseOpenedOrClosed(bool opened)
    {
        base.RaiseOpenedOrClosed(opened);
        if (opened)
            Opened?.Invoke(this, DateOpenedArgs);
        else
            Closed?.Invoke(this, DateClosedArgs);
    }

    /// <summary>Sets <see cref="SkUiPickerBase.Title"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TitleColor"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TextColor"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontFamily"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontSize"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAttributes"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CharacterSpacing"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.HorizontalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.VerticalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsOpen"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public new SkUiDatePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPlacement"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupBounds"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPresenter"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.WheelFormat"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetWheelFormat(string? value) { base.SetWheelFormat(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.IsLooping"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetIsLooping(bool value) { base.SetIsLooping(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.AreColumnHeadersVisible"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetAreColumnHeadersVisible(bool value) { base.SetAreColumnHeadersVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiDateTimePickerBase.VisibleRows"/> (same as the property setter).</summary>
    public new SkUiDatePicker SetVisibleRows(int value) { base.SetVisibleRows(value); return this; }
}
