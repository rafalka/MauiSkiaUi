using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace MauiSkiaUi;

/// <summary>What follows a <see cref="SkUiColumnsTracker"/>: a picker's columns model, or the inline wheels.</summary>
internal interface ISkUiColumnsHost
{
    /// <summary>
    /// What the wheels show changed: columns added, removed or replaced, or a column's texts, loop, width, header or item template.
    /// Wheels built from the old columns go (a popup closes, the inline wheels are built again); the indices are read right after.
    /// </summary>
    void ColumnsRebuilt();

    /// <summary>The chosen indices were read again (<see cref="SkUiColumnsTracker.SelectedIndices"/>).</summary>
    void SelectionRefreshed();

    /// <summary>Which items of <paramref name="column"/> can be chosen changed.</summary>
    void ItemStatesChanged(int column, IReadOnlyList<bool>? states);

    /// <summary>The chosen index of <paramref name="column"/> changed from <paramref name="previous"/> to <paramref name="current"/>.</summary>
    void SelectionChanged(int column, int previous, int current);
}

/// <summary>
/// Follows a list of <see cref="SkUiPickerColumn"/>s (each weakly: a column may be shared) and their chosen indices for a host
/// (<see cref="ISkUiColumnsHost"/>): the multi-column pickers on both layers (<see cref="SkUiColumnsModel"/>) and
/// <see cref="SkUiPickerWheels"/>.
/// </summary>
internal sealed class SkUiColumnsTracker
{
    private readonly ISkUiColumnsHost _host;
    private readonly List<SkUiWeakListener<SkUiColumnsTracker>> _listeners = [];
    private int[] _indices = [];
    private bool _applying;

    public SkUiColumnsTracker(ISkUiColumnsHost host)
    {
        _host = host;
        Columns.CollectionChanged += OnColumnsCollectionChanged;
    }

    /// <summary>The columns, left to right (right to left in right-to-left layouts).</summary>
    public ObservableCollection<SkUiPickerColumn> Columns { get; } = [];

    /// <summary>The chosen index of each column (-1: none), as of the last change.</summary>
    public IReadOnlyList<int> SelectedIndices => _indices;

    /// <summary>Raised after the columns changed (added, removed, replaced).</summary>
    public event EventHandler? ColumnsChanged;

    /// <summary>Reads the columns' indices again. <paramref name="raise"/>: tells the host which indices changed.</summary>
    public void Refresh(bool raise)
    {
        var previous = _indices;
        var next = new int[Columns.Count];
        for (var column = 0; column < next.Length; column++)
            next[column] = Columns[column].SelectedIndex;
        _indices = next;
        _host.SelectionRefreshed();
        if (!raise)
            return;
        for (var column = 0; column < next.Length; column++)
            if (column < previous.Length && previous[column] != next[column])
                _host.SelectionChanged(column, previous[column], next[column]);
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        foreach (var listener in _listeners)
            listener.Listen(null);
        _listeners.Clear();
        foreach (var column in Columns)
        {
            var listener = new SkUiWeakListener<SkUiColumnsTracker>(this, static (tracker, change) => tracker.OnColumnChanged(change));
            listener.Listen(column);
            _listeners.Add(listener);
        }
        _host.ColumnsRebuilt();
        Refresh(raise: false);
        ColumnsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnColumnChanged(SkUiChange change)
    {
        switch (change.PropertyName)
        {
            // Once the index and the item agree (the column's selection notice, not the index's change).
            case SkUiPickerColumn.SelectionChanged when !_applying:
                Refresh(raise: true);
                break;
            case SkUiPickerColumn.ItemStatesChanged when change.Sender is SkUiPickerColumn column && Columns.IndexOf(column) is var index and >= 0:
                _host.ItemStatesChanged(index, column.ItemStates);
                break;
            case SkUiPickerColumn.TextsChanged or nameof(SkUiPickerColumn.Loop) or nameof(SkUiPickerColumn.Width) or nameof(SkUiPickerColumn.Header)
                or nameof(SkUiPickerColumn.ItemTemplate):
                _host.ColumnsRebuilt();
                // While Apply writes the columns (an app's handler changing items), its own refresh at the end raises the changes.
                if (!_applying)
                    Refresh(raise: true);
                break;
        }
    }

    /// <summary>
    /// What wheels built now show of the columns: texts, loops, relative widths (-1: the widest text), item states, and the parts
    /// besides the texts (a column is a bindable object: MAUI's semantic properties set on it name and describe its wheel).
    /// </summary>
    public (string[][] Texts, bool[] Loops, double[] Widths, IReadOnlyList<bool>?[] States, SkUiWheelsView.ColumnParts[] Parts) Snapshot()
    {
        var texts = new string[Columns.Count][];
        var loops = new bool[texts.Length];
        var widths = new double[texts.Length];
        var states = new IReadOnlyList<bool>?[texts.Length];
        var parts = new SkUiWheelsView.ColumnParts[texts.Length];
        for (var column = 0; column < texts.Length; column++)
        {
            var source = Columns[column];
            texts[column] = source.Texts;
            states[column] = source.ItemStates;
            parts[column] = new SkUiWheelsView.ColumnParts(source.Header, source.ItemTemplate, source.ItemsSource,
                SemanticProperties.GetDescription(source), SemanticProperties.GetHint(source));
            loops[column] = source.Loop;
            widths[column] = source.Width;
        }
        return (texts, loops, widths, states, parts);
    }

    /// <summary>
    /// The wheels chose these indices (one per column): they become the columns' selection (written as handler values). Every column
    /// is written before the host hears of the changes, so the picker's <c>SelectionChanged</c> never reports half a choice (new hours
    /// with old minutes). Each column's own property changes (and bindings to them) still come one column at a time. The host is told
    /// also when writing a column throws (an app's handler), for the columns written before it.
    /// </summary>
    public void Apply(IReadOnlyList<int> indices)
    {
        _applying = true;
        try
        {
            for (var column = 0; column < indices.Count && column < Columns.Count; column++)
                Columns[column].Choose(indices[column]);
        }
        finally
        {
            _applying = false;
            Refresh(raise: true);
        }
    }
}
