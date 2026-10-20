using System.Collections.ObjectModel;
using System.Windows.Input;

namespace MauiSkiaUi;

/// <summary>Data of <see cref="SkUiMultiColumnPicker.SelectionChanged"/>: which column chose another item.</summary>
public sealed class SkUiColumnSelectionChangedEventArgs(int columnIndex, int previousIndex, int currentIndex) : EventArgs
{
    /// <summary>The column that changed.</summary>
    public int ColumnIndex { get; } = columnIndex;

    /// <summary>The column's chosen index before (-1: none).</summary>
    public int PreviousIndex { get; } = previousIndex;

    /// <summary>The column's chosen index now (-1: none).</summary>
    public int CurrentIndex { get; } = currentIndex;
}

/// <summary>
/// A drawn picker with several columns: the field shows the chosen item of each column joined (<see cref="Separator"/>, or
/// <see cref="Format"/>), and the popup holds a wheel for each column (a snapping, optionally looping list with the chosen item in
/// the middle row), on the app's popup system like <see cref="SkUiPicker"/>'s list. It is the base of dates, times and any choice
/// of several parts (a hex color's channels, hours and minutes, a unit and an amount). Fill <see cref="Columns"/> with
/// <see cref="SkUiPickerColumn"/>s (in XAML, the element's content).
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing.</b> Scrolling a wheel (a drag, a fling, the pointer wheel), tapping an item, or the keyboard changes the column's
/// <see cref="SkUiPickerColumn.SelectedIndex"/> at once, as the wheel's middle row changes; the popup stays open and closes on a
/// dismissal (the presenter's tap outside, Back), Enter, Space or Escape, keeping what was chosen. A column with nothing chosen
/// shows its first item in the middle row until the user scrolls or taps.
/// </para>
/// <para>
/// <b>Keyboard</b> (in the popup, which takes focus when shown): Left / Right change the column, Up / Down, Page Up / Page Down,
/// Home / End move its wheel, typing jumps to the next item starting with what was typed. The closed field opens with a tap,
/// Space, Enter or Alt+Down.
/// </para>
/// <para>
/// The field, fonts, colors, <see cref="SkUiPickerBase.IsOpen"/> and the popup's placement are the picker's
/// (<see cref="SkUiPickerBase"/>). Core twin: <see cref="Core.SkUiCoreMultiColumnPicker"/>.
/// </para>
/// </remarks>
[ContentProperty(nameof(Columns))]
public class SkUiMultiColumnPicker : SkUiPickerBase
{
    /// <summary>The default for <see cref="Separator"/>: a space.</summary>
    public const string DefaultSeparator = SkUiColumnsModel.DefaultSeparator;

    private SkUiColumnsModel? _columns;

    /// <summary>Creates a picker without columns.</summary>
    public SkUiMultiColumnPicker()
    {
    }

    /// <summary>The columns' engine, created with the picker's (property callbacks can run before the constructor).</summary>
    private SkUiColumnsModel ColumnsModel
    {
        get
        {
            _ = Model;
            return _columns!;
        }
    }

    private protected override void OnModelCreated(SkUiPickerModel model)
    {
        _columns = new SkUiColumnsModel(model, (column, previous, current) =>
        {
            model.RunSelectionChangedCommand();
            SelectionChanged?.Invoke(this, new SkUiColumnSelectionChangedEventArgs(column, previous, current));
        });
        _columns.ColumnsChanged += (_, _) => InheritBindingContext();
    }

    /// <summary>Bindable property for <see cref="Separator"/>.</summary>
    public static readonly BindableProperty SeparatorProperty = BindableProperty.Create(nameof(Separator), typeof(string), typeof(SkUiMultiColumnPicker),
        DefaultSeparator, validateValue: SkUiValidate.NotNull,
        propertyChanged: (picker, _, value) => ((SkUiMultiColumnPicker)picker).ApplyText(model => model.Separator = (string)value));

    /// <summary>Bindable property for <see cref="Format"/>.</summary>
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(SkUiMultiColumnPicker), null,
        propertyChanged: (picker, _, value) => ((SkUiMultiColumnPicker)picker).ApplyText(model => model.Format = (string?)value));

    /// <summary>Bindable property for <see cref="VisibleRows"/>.</summary>
    public static readonly BindableProperty VisibleRowsProperty = BindableProperty.Create(nameof(VisibleRows), typeof(int), typeof(SkUiMultiColumnPicker),
        SkUiColumnsModel.DefaultVisibleRows, validateValue: (_, value) => (int)value is >= 1 and <= SkUiColumnsModel.MaximumVisibleRows,
        propertyChanged: (picker, _, value) => ((SkUiMultiColumnPicker)picker).ColumnsModel.VisibleRows = (int)value);

    /// <summary>
    /// The columns, left to right (right to left in right-to-left layouts). Changing them closes an open popup. In XAML, the
    /// element's content.
    /// </summary>
    public IList<SkUiPickerColumn> Columns => ColumnsModel.Columns;

    /// <summary>
    /// What goes between the chosen texts in the field (default <c>" "</c>); columns with nothing chosen add nothing. Ignored with
    /// a <see cref="Format"/>.
    /// </summary>
    public string Separator { get => (string)GetValue(SeparatorProperty); set => SetValue(SeparatorProperty, value); }

    /// <summary>
    /// A <c>string.Format</c> format of the field, with the chosen text of each column as <c>{0}</c>, <c>{1}</c>, … (an empty text
    /// for a column with nothing chosen): <c>"{0}:{1}"</c>. <c>null</c> (default): the texts joined by <see cref="Separator"/>.
    /// </summary>
    public string? Format { get => (string?)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <summary>How many rows of each wheel show (default 5; 1–9). Odd numbers put the chosen row in the middle.</summary>
    public int VisibleRows { get => (int)GetValue(VisibleRowsProperty); set => SetValue(VisibleRowsProperty, value); }

    /// <summary>The chosen index of each column (-1: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<int> SelectedIndices => ColumnsModel.SelectedIndices;

    /// <summary>The chosen item of each column (<c>null</c>: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<object?> SelectedItems => [.. Columns.Select(column => column.SelectedItem)];

    /// <summary>Raised for each column that chose another item (the user in the popup, or the app).</summary>
    public event EventHandler<SkUiColumnSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Sets <see cref="Separator"/> (same as the property setter).</summary>
    public SkUiMultiColumnPicker SetSeparator(string value) { ArgumentNullException.ThrowIfNull(value); Separator = value; return this; }

    /// <summary>Sets <see cref="Format"/> (same as the property setter).</summary>
    public SkUiMultiColumnPicker SetFormat(string? value) { Format = value; return this; }

    /// <summary>Sets <see cref="VisibleRows"/> (same as the property setter).</summary>
    public SkUiMultiColumnPicker SetVisibleRows(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiColumnsModel.MaximumVisibleRows);
        VisibleRows = value;
        return this;
    }

    /// <summary>Sets <see cref="SkUiPickerBase.Title"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TitleColor"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.TextColor"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontFamily"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontSize"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAttributes"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.FontAutoScalingEnabled"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CharacterSpacing"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.HorizontalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.VerticalTextAlignment"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsOpen"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupHeight"/> (same as the property setter; -1: no limit).</summary>
    public new SkUiMultiColumnPicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPlacement"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupBounds"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupPresenter"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MinPopupWidth"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetMinPopupWidth(double value) { base.SetMinPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.MaxPopupWidth"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetMaxPopupWidth(double value) { base.SetMaxPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetFieldStyle" />
    public new SkUiMultiColumnPicker SetFieldStyle(SkUiPickerFieldStyle value) { base.SetFieldStyle(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetFillColor" />
    public new SkUiMultiColumnPicker SetFillColor(Color? value) { base.SetFillColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetBorderColor" />
    public new SkUiMultiColumnPicker SetBorderColor(Color? value) { base.SetBorderColor(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetBorderWidth" />
    public new SkUiMultiColumnPicker SetBorderWidth(double? value) { base.SetBorderWidth(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetCornerRadii" />
    public new SkUiMultiColumnPicker SetCornerRadii(CornerRadius? value) { base.SetCornerRadii(value); return this; }

    /// <inheritdoc cref="SkUiPickerBase.SetCornerRadius" />
    public new SkUiMultiColumnPicker SetCornerRadius(int? value) { base.SetCornerRadius(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiPickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiMultiColumnPicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <inheritdoc />
    private protected override void ClearChoice()
    {
        foreach (var column in Columns)
            column.SelectedIndex = -1;
    }

    /// <summary>Adds <paramref name="column"/> after the others.</summary>
    public SkUiMultiColumnPicker AddColumn(SkUiPickerColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        Columns.Add(column);
        return this;
    }

    private void ApplyText(Action<SkUiColumnsModel> apply)
    {
        apply(ColumnsModel);
        ColumnsModel.RefreshText();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext();
    }

    /// <summary>The columns bind to the picker's binding context (XAML columns bind to the view model).</summary>
    private void InheritBindingContext()
    {
        foreach (var column in Columns)
            SetInheritedBindingContext(column, BindingContext);
    }
}
