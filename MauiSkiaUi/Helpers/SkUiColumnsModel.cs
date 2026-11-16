using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// The columns of a multi-column picker, on either layer (<see cref="SkUiMultiColumnPicker"/>,
/// <see cref="Core.SkUiCoreMultiColumnPicker"/>): follows the <see cref="Columns"/> (<see cref="SkUiColumnsTracker"/>), works out the
/// field's text from the chosen items, tells the control which column changed, and builds the wheel popup on
/// <see cref="SkUiPickerModel"/>'s popup session.
/// </summary>
internal sealed class SkUiColumnsModel : ISkUiColumnsHost
{
    /// <summary>The default for <see cref="Separator"/>.</summary>
    internal const string DefaultSeparator = " ";

    /// <summary>The default for <see cref="VisibleRows"/>.</summary>
    internal const int DefaultVisibleRows = 5;

    /// <summary>The most rows a wheel shows.</summary>
    internal const int MaximumVisibleRows = 9;

    private readonly SkUiPickerModel _model;
    private readonly Action<int, int, int> _raiseChanged;
    private readonly SkUiColumnsTracker _tracker;

    /// <param name="model">The field and popup engine of the control.</param>
    /// <param name="raiseChanged">Tells the control that the chosen index of a column changed (column, previous, current).</param>
    public SkUiColumnsModel(SkUiPickerModel model, Action<int, int, int> raiseChanged)
    {
        _model = model;
        _raiseChanged = raiseChanged;
        model.UsesValueText = true;
        model.ClosedFieldSelects = false;
        model.ContentFactory = CreateContent;
        _tracker = new SkUiColumnsTracker(this);
    }

    /// <summary>The columns, left to right in the popup (right to left in right-to-left layouts).</summary>
    public ObservableCollection<SkUiPickerColumn> Columns => _tracker.Columns;

    /// <summary>What goes between the chosen texts in the field when <see cref="Format"/> is not set.</summary>
    public string Separator = DefaultSeparator;

    /// <summary><c>string.Format</c>'s format of the field, with the chosen text of each column as <c>{0}</c>, <c>{1}</c>, …; <c>null</c>: joined by <see cref="Separator"/>.</summary>
    public string? Format;

    /// <summary>How many rows of each wheel show in the popup (odd; the middle one is chosen).</summary>
    public int VisibleRows = DefaultVisibleRows;

    /// <summary>The chosen index of each column (-1: none), as of the last change.</summary>
    public IReadOnlyList<int> SelectedIndices => _tracker.SelectedIndices;

    /// <summary>Raised after the columns changed (added, removed, replaced).</summary>
    public event EventHandler? ColumnsChanged
    {
        add => _tracker.ColumnsChanged += value;
        remove => _tracker.ColumnsChanged -= value;
    }

    /// <summary>Reads the columns again: the indices, the field's text, and an open popup's wheels. <paramref name="raise"/>: tells the control which indices changed.</summary>
    public void Refresh(bool raise) => _tracker.Refresh(raise);

    /// <summary>The formatting properties changed: the field's text again.</summary>
    public void RefreshText()
    {
        _model.ValueText = Compose(_tracker.SelectedIndices);
        _model.TextChanged();
    }

    private string? Compose(IReadOnlyList<int> indices)
    {
        var chosen = false;
        var texts = new object[indices.Count];
        for (var column = 0; column < indices.Count; column++)
        {
            var text = indices[column] >= 0 && indices[column] < Columns[column].Texts.Length ? Columns[column].Texts[indices[column]] : string.Empty;
            chosen |= indices[column] >= 0;
            texts[column] = text;
        }
        if (!chosen)
            return null;
        if (Format is { } format)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, format, texts);
            }
            catch (FormatException)
            {
                // A format that names more columns than there are (columns added after it), or a malformed one: the texts joined.
            }
        }
        var shown = new List<string>(texts.Length);
        foreach (var text in texts)
            if (text is string { Length: > 0 } value)
                shown.Add(value);
        return string.Join(Separator, shown);
    }

    #region ISkUiColumnsHost

    /// <inheritdoc />
    /// <remarks>The popup shows the columns it opened with: it closes.</remarks>
    void ISkUiColumnsHost.ColumnsRebuilt() => _model.Close();

    /// <inheritdoc />
    void ISkUiColumnsHost.SelectionRefreshed()
    {
        _model.ValueText = Compose(_tracker.SelectedIndices);
        _model.SelectionChanged();
    }

    /// <inheritdoc />
    /// <remarks>An open popup follows (its wheels move off items now disabled).</remarks>
    void ISkUiColumnsHost.ItemStatesChanged(int column, IReadOnlyList<bool>? states) => _model.ColumnItemStatesChanged(column, states);

    /// <inheritdoc />
    void ISkUiColumnsHost.SelectionChanged(int column, int previous, int current) => _raiseChanged(column, previous, current);

    #endregion

    #region Popup

    private ISkUiPickerPopupContent CreateContent(SkUiPickerPopupContext context)
    {
        var (texts, loops, widths, states, columns) = _tracker.Snapshot();
        // Weak: a request kept by a presenter (a popup still shown after its page went) keeps no picker alive.
        var weak = new WeakReference<SkUiColumnsModel>(this);
        return new SkUiColumnsPopup(texts, loops, widths, _tracker.SelectedIndices, Math.Max(1, VisibleRows), context.RowStyle, context.Title,
            context.RightToLeft, context.Chrome, indices =>
            {
                if (weak.TryGetTarget(out var model))
                    model._tracker.Apply(indices);
            },
            () => weak.TryGetTarget(out var model) ? model._tracker.SelectedIndices : [], states, columns, context.Extras);
    }

    #endregion
}
