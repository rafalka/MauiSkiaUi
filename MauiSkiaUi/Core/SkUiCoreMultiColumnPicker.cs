using System.Windows.Input;

namespace MauiSkiaUi.Core;

/// <summary>
/// Drawn picker with several columns (Core analogue of <see cref="SkUiMultiColumnPicker"/>): the same field, the same wheel popup on the
/// app's popup system, the same <see cref="SkUiPickerColumn"/>s. Columns are bindable objects (they can bind to a view model
/// through their own bindings, and one column can serve a <see cref="SkUiMultiColumnPicker"/> as well); the picker listens to them weakly.
/// </summary>
/// <remarks>Core nodes have no enabled state: the picker always reacts (hide it, or remove it, to stop it).</remarks>
public class SkUiCoreMultiColumnPicker : SkUiCorePickerBase
{
    private readonly SkUiColumnsModel _columns;

    /// <summary>Creates a picker without columns.</summary>
    public SkUiCoreMultiColumnPicker() =>
        _columns = new SkUiColumnsModel(Model, (column, previous, current) =>
        {
            Model.RunSelectionChangedCommand();
            SelectionChanged?.Invoke(this, new SkUiColumnSelectionChangedEventArgs(column, previous, current));
        });

    /// <summary>The columns, left to right (right to left in right-to-left layouts). Changing them closes an open popup.</summary>
    public IList<SkUiPickerColumn> Columns => _columns.Columns;

    /// <summary>What goes between the chosen texts in the field (default <c>" "</c>); ignored with a <see cref="Format"/>.</summary>
    public string Separator { get => _columns.Separator; set => SetSeparator(value); }

    /// <summary>A <c>string.Format</c> format of the field, with the chosen text of each column as <c>{0}</c>, <c>{1}</c>, …; <c>null</c> (default): the texts joined by <see cref="Separator"/>.</summary>
    public string? Format { get => _columns.Format; set => SetFormat(value); }

    /// <summary>How many rows of each wheel show (default 5; 1–9).</summary>
    public int VisibleRows { get => _columns.VisibleRows; set => SetVisibleRows(value); }

    /// <summary>The chosen index of each column (-1: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<int> SelectedIndices => _columns.SelectedIndices;

    /// <summary>The chosen item of each column (<c>null</c>: none), in the order of <see cref="Columns"/>.</summary>
    public IReadOnlyList<object?> SelectedItems => [.. Columns.Select(column => column.SelectedItem)];

    /// <summary>Raised for each column that chose another item (the user in the popup, or the app).</summary>
    public event EventHandler<SkUiColumnSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Sets <see cref="Separator"/>.</summary>
    public SkUiCoreMultiColumnPicker SetSeparator(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref _columns.Separator, value, nameof(Separator)))
            _columns.RefreshText();
        return this;
    }

    /// <summary>Sets <see cref="Format"/>.</summary>
    public SkUiCoreMultiColumnPicker SetFormat(string? value)
    {
        if (SetProperty(ref _columns.Format, value, nameof(Format)))
            _columns.RefreshText();
        return this;
    }

    /// <summary>Sets <see cref="VisibleRows"/> (1–9).</summary>
    public SkUiCoreMultiColumnPicker SetVisibleRows(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, SkUiColumnsModel.MaximumVisibleRows);
        SetProperty(ref _columns.VisibleRows, value, nameof(VisibleRows));
        return this;
    }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Title"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetTitle(string? value) { base.SetTitle(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TitleColor"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetTitleColor(Color? value) { base.SetTitleColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.TextColor"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetTextColor(Color? value) { base.SetTextColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontFamily"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetFontFamily(string? value) { base.SetFontFamily(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontSize"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetFontSize(double value) { base.SetFontSize(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAttributes"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetFontAttributes(FontAttributes value) { base.SetFontAttributes(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.FontAutoScalingEnabled"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetFontAutoScalingEnabled(bool value) { base.SetFontAutoScalingEnabled(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CharacterSpacing"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetCharacterSpacing(double value) { base.SetCharacterSpacing(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.HorizontalTextAlignment"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetHorizontalTextAlignment(TextAlignment value) { base.SetHorizontalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.VerticalTextAlignment"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetVerticalTextAlignment(TextAlignment value) { base.SetVerticalTextAlignment(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsOpen"/> (opens or closes the list).</summary>
    public new SkUiCoreMultiColumnPicker SetIsOpen(bool value) { base.SetIsOpen(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.MaxPopupHeight"/> (-1: no limit).</summary>
    public new SkUiCoreMultiColumnPicker SetMaxPopupHeight(double value) { base.SetMaxPopupHeight(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPlacement"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetPopupPlacement(SkUiPopupPlacement value) { base.SetPopupPlacement(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupBounds"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetPopupBounds(Rect value) { base.SetPopupBounds(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupPresenter"/>.</summary>
    public new SkUiCoreMultiColumnPicker SetPopupPresenter(ISkUiPopupPresenter? value) { base.SetPopupPresenter(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.UpdateMode"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetUpdateMode(SkUiPickerUpdateMode value) { base.SetUpdateMode(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.AcceptText"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetAcceptText(string? value) { base.SetAcceptText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.CancelText"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetCancelText(string? value) { base.SetCancelText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupHeaderTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetPopupHeaderTemplate(DataTemplate? value) { base.SetPopupHeaderTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupFooterTemplate"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetPopupFooterTemplate(DataTemplate? value) { base.SetPopupFooterTemplate(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetIsClearButtonVisible(bool value) { base.SetIsClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.IsPopupClearButtonVisible"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetIsPopupClearButtonVisible(bool value) { base.SetIsPopupClearButtonVisible(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.ClearText"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetClearText(string? value) { base.SetClearText(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.Placeholder"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetPlaceholder(string? value) { base.SetPlaceholder(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PlaceholderColor"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetPlaceholderColor(Color? value) { base.SetPlaceholderColor(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.PopupWidth"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetPopupWidth(double value) { base.SetPopupWidth(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommand"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetSelectionChangedCommand(ICommand? value) { base.SetSelectionChangedCommand(value); return this; }

    /// <summary>Sets <see cref="SkUiCorePickerBase.SelectionChangedCommandParameter"/> (same as the property setter).</summary>
    public new SkUiCoreMultiColumnPicker SetSelectionChangedCommandParameter(object? value) { base.SetSelectionChangedCommandParameter(value); return this; }

    /// <inheritdoc />
    private protected override void ClearChoice()
    {
        foreach (var column in Columns)
            column.SelectedIndex = -1;
    }

    /// <summary>Adds <paramref name="column"/> after the others.</summary>
    public SkUiCoreMultiColumnPicker AddColumn(SkUiPickerColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        Columns.Add(column);
        return this;
    }
}
