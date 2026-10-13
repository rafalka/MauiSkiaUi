using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;

namespace MauiSkiaUi;

/// <summary>
/// The columns of a multi-column picker, on either layer (<see cref="SkUiMultiColumnPicker"/>,
/// <see cref="Core.SkUiCoreMultiColumnPicker"/>): follows the <see cref="Columns"/> and each column (weakly, a column may be shared),
/// works out the field's text from the chosen items, tells the control which column changed, and builds the wheel popup on
/// <see cref="SkUiPickerModel"/>'s popup session.
/// </summary>
internal sealed class SkUiColumnsModel
{
    /// <summary>The default for <see cref="Separator"/>.</summary>
    internal const string DefaultSeparator = " ";

    /// <summary>The default for <see cref="VisibleRows"/>.</summary>
    internal const int DefaultVisibleRows = 5;

    /// <summary>The most rows a wheel shows.</summary>
    internal const int MaximumVisibleRows = 9;

    private readonly SkUiPickerModel _model;
    private readonly Action<int, int, int> _raiseChanged;
    private readonly List<SkUiWeakListener<SkUiColumnsModel>> _listeners = [];
    private int[] _indices = [];

    /// <param name="model">The field and popup engine of the control.</param>
    /// <param name="raiseChanged">Tells the control that the chosen index of a column changed (column, previous, current).</param>
    public SkUiColumnsModel(SkUiPickerModel model, Action<int, int, int> raiseChanged)
    {
        _model = model;
        _raiseChanged = raiseChanged;
        model.UsesValueText = true;
        model.ClosedFieldSelects = false;
        model.ContentFactory = CreateContent;
        Columns.CollectionChanged += OnColumnsCollectionChanged;
    }

    /// <summary>The columns, left to right in the popup (right to left in right-to-left layouts).</summary>
    public ObservableCollection<SkUiPickerColumn> Columns { get; } = [];

    /// <summary>What goes between the chosen texts in the field when <see cref="Format"/> is not set.</summary>
    public string Separator = DefaultSeparator;

    /// <summary><c>string.Format</c>'s format of the field, with the chosen text of each column as <c>{0}</c>, <c>{1}</c>, …; <c>null</c>: joined by <see cref="Separator"/>.</summary>
    public string? Format;

    /// <summary>How many rows of each wheel show in the popup (odd; the middle one is chosen).</summary>
    public int VisibleRows = DefaultVisibleRows;

    /// <summary>The chosen index of each column (-1: none), as of the last change.</summary>
    public IReadOnlyList<int> SelectedIndices => _indices;

    /// <summary>Raised after the columns changed (added, removed, replaced).</summary>
    public event EventHandler? ColumnsChanged;

    /// <summary>Reads the columns again: the indices, the field's text, and an open popup's wheels. <paramref name="raise"/>: tells the control which indices changed.</summary>
    public void Refresh(bool raise)
    {
        var previous = _indices;
        var next = new int[Columns.Count];
        for (var column = 0; column < next.Length; column++)
            next[column] = Columns[column].SelectedIndex;
        _indices = next;
        _model.ValueText = Compose(next);
        _model.SelectionChanged();
        if (!raise)
            return;
        for (var column = 0; column < next.Length; column++)
            if (column < previous.Length && previous[column] != next[column])
                _raiseChanged(column, previous[column], next[column]);
    }

    /// <summary>The formatting properties changed: the field's text again.</summary>
    public void RefreshText()
    {
        _model.ValueText = Compose(_indices);
        _model.TextChanged();
    }

    private string? Compose(int[] indices)
    {
        var chosen = false;
        var texts = new object[indices.Length];
        for (var column = 0; column < indices.Length; column++)
        {
            var text = indices[column] >= 0 && indices[column] < Columns[column].Texts.Length ? Columns[column].Texts[indices[column]] : string.Empty;
            chosen |= indices[column] >= 0;
            texts[column] = text;
        }
        if (!chosen)
            return null;
        if (Format is { } format)
            return string.Format(CultureInfo.CurrentCulture, format, texts);
        var shown = new List<string>(texts.Length);
        foreach (var text in texts)
            if (text is string { Length: > 0 } value)
                shown.Add(value);
        return string.Join(Separator, shown);
    }

    #region Following the columns

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (var listener in _listeners)
            listener.Listen(null);
        _listeners.Clear();
        foreach (var column in Columns)
        {
            var listener = new SkUiWeakListener<SkUiColumnsModel>(this, static (model, change) => model.OnColumnChanged(change));
            listener.Listen(column);
            _listeners.Add(listener);
        }
        // The popup shows the columns it opened with.
        _model.Close();
        Refresh(raise: false);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnChanged(SkUiChange change)
    {
        switch (change.PropertyName)
        {
            case nameof(SkUiPickerColumn.SelectedIndex):
                Refresh(raise: true);
                break;
            case SkUiPickerColumn.ItemStatesChanged when change.Sender is SkUiPickerColumn column && Columns.IndexOf(column) is var index and >= 0:
                // An open popup follows (its wheels move off items now disabled).
                _model.ColumnItemStatesChanged(index, column.ItemStates);
                break;
            case SkUiPickerColumn.TextsChanged or nameof(SkUiPickerColumn.Loop) or nameof(SkUiPickerColumn.Width):
                _model.Close();
                Refresh(raise: true);
                break;
        }
    }

    #endregion

    #region Popup

    private ISkUiPickerPopupContent CreateContent(SkUiPickerPopupContext context)
    {
        var texts = new string[Columns.Count][];
        var loops = new bool[texts.Length];
        var widths = new double[texts.Length];
        var states = new IReadOnlyList<bool>?[texts.Length];
        for (var column = 0; column < texts.Length; column++)
        {
            texts[column] = Columns[column].Texts;
            states[column] = Columns[column].ItemStates;
            loops[column] = Columns[column].Loop;
            widths[column] = Columns[column].Width;
        }
        // Weak: a request kept by a presenter (a popup still shown after its page went) keeps no picker alive.
        var weak = new WeakReference<SkUiColumnsModel>(this);
        return new SkUiColumnsPopup(texts, loops, widths, _indices, Math.Max(1, VisibleRows), context.RowStyle, context.Title, context.RightToLeft,
            context.Chrome, indices =>
            {
                if (weak.TryGetTarget(out var model))
                    model.Apply(indices);
            },
            () => weak.TryGetTarget(out var model) ? model._indices : [], states);
    }

    /// <summary>The popup chose these indices (one per column): they become the columns' selection.</summary>
    private void Apply(IReadOnlyList<int> indices)
    {
        for (var column = 0; column < indices.Count && column < Columns.Count; column++)
            if (Columns[column].SelectedIndex != indices[column])
                Columns[column].SelectedIndex = indices[column];
    }

    #endregion
}
