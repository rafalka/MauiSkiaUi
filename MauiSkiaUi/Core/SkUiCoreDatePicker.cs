using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn date picker (Core analogue of <see cref="SkUiDatePicker"/>): the same field, wheels and rules (MAUI's date, kept in the
/// range; blackout dates, a rule of the app's, the highlighted date, the wheel format), set with fluent <c>Set*</c> calls.
/// </summary>
/// <remarks>Core nodes have no enabled state: the picker always reacts (hide it, or remove it, to stop it).</remarks>
public class SkUiCoreDatePicker : SkUiCoreDateTimePickerBase
{
    private DateTime? _date = DateTime.Today;
    private DateTime? _minimumDate = SkUiDateTimeModel.DefaultMinimumDate;
    private DateTime? _maximumDate = SkUiDateTimeModel.DefaultMaximumDate;
    private DateTime? _defaultHighlightedDate;
    private IList<DateTime> _blackoutDates = [];
    private Func<DateTime, bool>? _dateEnabled;

    /// <summary>Creates a picker of today's date.</summary>
    public SkUiCoreDatePicker() : base(SkUiDateTimeKind.Date)
    {
        Dates.Value = _date;
        Dates.BlackoutDates = _blackoutDates;
        Dates.TextChanged();
    }

    /// <summary>The chosen date (its time is midnight); <c>null</c>: none. Today by default. Kept between <see cref="MinimumDate"/> and <see cref="MaximumDate"/>.</summary>
    public DateTime? Date { get => _date; set => SetDate(value); }

    /// <summary>The earliest date that can be chosen (default 1 January 1900); <c>null</c>: none.</summary>
    public DateTime? MinimumDate { get => _minimumDate; set => SetMinimumDate(value); }

    /// <summary>The latest date that can be chosen (default 31 December 2100); <c>null</c>: none.</summary>
    public DateTime? MaximumDate { get => _maximumDate; set => SetMaximumDate(value); }

    /// <summary>The field's .NET date format (default <c>"d"</c>).</summary>
    public string Format { get => Dates.Format; set => SetFormat(value); }

    /// <summary>The date the wheels show while <see cref="Date"/> is <c>null</c>; <c>null</c> (default): today.</summary>
    public DateTime? DefaultHighlightedDate { get => _defaultHighlightedDate; set => SetDefaultHighlightedDate(value); }

    /// <summary>Days that cannot be chosen (an empty list by default; add to it, or set your own; read when the popup opens).</summary>
    public IList<DateTime> BlackoutDates { get => _blackoutDates; set => SetBlackoutDates(value); }

    /// <summary>Whether a day can be chosen, a rule of the app's; <c>null</c> (default): every day in the range.</summary>
    public Func<DateTime, bool>? DateEnabled { get => _dateEnabled; set => SetDateEnabled(value); }

    /// <summary>Raised when <see cref="Date"/> changes (the user, or the app), with the dates before and after.</summary>
    public event EventHandler<DateChangedEventArgs>? DateSelected;

    /// <summary>Sets <see cref="Date"/> (its day, kept in the range).</summary>
    public SkUiCoreDatePicker SetDate(DateTime? value)
    {
        var date = value?.Date;
        if (date > _maximumDate)
            date = _maximumDate;
        if (date < _minimumDate)
            date = _minimumDate;
        var previous = _date;
        if (!SetProperty(ref _date, date, nameof(Date)))
            return this;
        Dates.Value = date;
        Dates.ValueChanged();
        Model.RunSelectionChangedCommand();
        DateSelected?.Invoke(this, new DateChangedEventArgs(previous, date));
        return this;
    }

    /// <summary>Sets <see cref="MinimumDate"/> (its day); a date before it moves to it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is after <see cref="MaximumDate"/>.</exception>
    public SkUiCoreDatePicker SetMinimumDate(DateTime? value)
    {
        if (value?.Date > _maximumDate)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum date must not be after the maximum date.");
        if (!SetProperty(ref _minimumDate, value?.Date, nameof(MinimumDate)))
            return this;
        Dates.Minimum = _minimumDate ?? DateTime.MinValue;
        Wheels(true);
        return SetDate(_date);
    }

    /// <summary>Sets <see cref="MaximumDate"/> (its day); a date after it moves to it.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is before <see cref="MinimumDate"/>.</exception>
    public SkUiCoreDatePicker SetMaximumDate(DateTime? value)
    {
        if (value?.Date < _minimumDate)
            throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum date must not be before the minimum date.");
        if (!SetProperty(ref _maximumDate, value?.Date, nameof(MaximumDate)))
            return this;
        Dates.Maximum = _maximumDate ?? DateTime.MaxValue.Date;
        Wheels(true);
        return SetDate(_date);
    }

    /// <summary>Sets <see cref="Format"/>.</summary>
    public SkUiCoreDatePicker SetFormat(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref Dates.Format, value, nameof(Format)))
            Dates.TextChanged();
        return this;
    }

    /// <summary>Sets <see cref="DefaultHighlightedDate"/>.</summary>
    public SkUiCoreDatePicker SetDefaultHighlightedDate(DateTime? value)
    {
        if (SetProperty(ref _defaultHighlightedDate, value?.Date, nameof(DefaultHighlightedDate)))
        {
            Dates.Highlighted = _defaultHighlightedDate;
            Wheels(true);
        }
        return this;
    }

    /// <summary>Sets <see cref="BlackoutDates"/>.</summary>
    public SkUiCoreDatePicker SetBlackoutDates(IList<DateTime> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _blackoutDates, value, nameof(BlackoutDates)))
        {
            Dates.BlackoutDates = value;
            Wheels(true);
        }
        return this;
    }

    /// <summary>Sets <see cref="DateEnabled"/>.</summary>
    public SkUiCoreDatePicker SetDateEnabled(Func<DateTime, bool>? value)
    {
        if (SetProperty(ref _dateEnabled, value, nameof(DateEnabled)))
        {
            Dates.DateEnabled = value;
            Wheels(true);
        }
        return this;
    }

    /// <inheritdoc />
    private protected override void ChooseValue(DateTime? value) => SetDate(value);

    /// <inheritdoc />
    private protected override void ClearChoice() => SetDate(null);

    /// <summary>Sets <see cref="SkUiCorePickerBase.Title"/>.</summary>
    public new SkUiCoreDatePicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TitleColor"/>.</summary>
    public new SkUiCoreDatePicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TextColor"/>.</summary>
    public new SkUiCoreDatePicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontFamily"/>.</summary>
    public new SkUiCoreDatePicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontSize"/>.</summary>
    public new SkUiCoreDatePicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAttributes"/>.</summary>
    public new SkUiCoreDatePicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCoreDatePicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CharacterSpacing"/>.</summary>
    public new SkUiCoreDatePicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.HorizontalTextAlignment"/>.</summary>
    public new SkUiCoreDatePicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.VerticalTextAlignment"/>.</summary>
    public new SkUiCoreDatePicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsOpen"/> (opens or closes the list).</summary>
    public new SkUiCoreDatePicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupHeight"/> (-1: no limit).</summary>
    public new SkUiCoreDatePicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPlacement"/>.</summary>
    public new SkUiCoreDatePicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupBounds"/>.</summary>
    public new SkUiCoreDatePicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPresenter"/>.</summary>
    public new SkUiCoreDatePicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFieldStyle" />
    public new SkUiCoreDatePicker SetFieldStyle(SkUiPickerFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetFillColor" />
    public new SkUiCoreDatePicker SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderColor" />
    public new SkUiCoreDatePicker SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetBorderWidth" />
    public new SkUiCoreDatePicker SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadii" />
    public new SkUiCoreDatePicker SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiCorePickerBase.SetCornerRadius" />
    public new SkUiCoreDatePicker SetCornerRadius(double value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiCoreDatePicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.WheelFormat"/>.</summary>
    public new SkUiCoreDatePicker SetWheelFormat(string? value) { base.SetWheelFormat(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.IsLooping"/>.</summary>
    public new SkUiCoreDatePicker SetIsLooping(bool value) { base.SetIsLooping(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.AreColumnHeadersVisible"/>.</summary>
    public new SkUiCoreDatePicker SetAreColumnHeadersVisible(bool value) { base.SetAreColumnHeadersVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCoreDateTimePickerBase.VisibleRows"/> (1–9).</summary>
    public new SkUiCoreDatePicker SetVisibleRows(int value) { base.SetVisibleRows(value); return this; }
}
