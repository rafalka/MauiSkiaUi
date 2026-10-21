using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>A drawn grid using MAUI's Auto, absolute, and star layout manager.</summary>
public class SkUiGrid : SkUiLayout, IGridLayout
{
    private readonly GridLayoutManager _manager;
    private RowDefinitionCollection _rows = new();
    private ColumnDefinitionCollection _columns = new();
    private double _rowSpacing;
    private double _columnSpacing;
    private IGridRowDefinition[]? _rowSnapshot;
    private IGridColumnDefinition[]? _columnSnapshot;

    /// <summary>Bindable row definitions, including XAML shorthand such as Auto,*.</summary>
    public static readonly BindableProperty RowDefinitionsProperty = BindableProperty.Create(
        nameof(RowDefinitions), typeof(RowDefinitionCollection), typeof(SkUiGrid), null,
        defaultValueCreator: view => ((SkUiGrid)view)._rows,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).OnRowDefinitionsChanged((RowDefinitionCollection)value));
    /// <summary>Bindable column definitions.</summary>
    public static readonly BindableProperty ColumnDefinitionsProperty = BindableProperty.Create(
        nameof(ColumnDefinitions), typeof(ColumnDefinitionCollection), typeof(SkUiGrid), null,
        defaultValueCreator: view => ((SkUiGrid)view)._columns,
        validateValue: SkUiValidate.NotNull,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).OnColumnDefinitionsChanged((ColumnDefinitionCollection)value));
    /// <summary>Bindable gap between rows.</summary>
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(
        nameof(RowSpacing), typeof(double), typeof(SkUiGrid), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).OnRowSpacingChanged((double)value));
    /// <summary>Bindable gap between columns.</summary>
    public static readonly BindableProperty ColumnSpacingProperty = BindableProperty.Create(
        nameof(ColumnSpacing), typeof(double), typeof(SkUiGrid), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).OnColumnSpacingChanged((double)value));

    /// <summary>Creates a grid with MAUI layout management.</summary>
    public SkUiGrid()
    {
        _manager = new GridLayoutManager(this);
        _rows.ItemSizeChanged += OnDefinitionsChanged;
        _columns.ItemSizeChanged += OnDefinitionsChanged;
    }

    /// <summary>Rows; an empty collection implies one star row.</summary>
    [System.ComponentModel.TypeConverter(typeof(RowDefinitionCollectionTypeConverter))]
    public RowDefinitionCollection RowDefinitions { get => (RowDefinitionCollection)GetValue(RowDefinitionsProperty); set => SetValue(RowDefinitionsProperty, value); }
    /// <summary>Columns; an empty collection implies one star column.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColumnDefinitionCollectionTypeConverter))]
    public ColumnDefinitionCollection ColumnDefinitions { get => (ColumnDefinitionCollection)GetValue(ColumnDefinitionsProperty); set => SetValue(ColumnDefinitionsProperty, value); }
    /// <summary>Gap between rows in DIPs.</summary>
    public double RowSpacing { get => (double)GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }
    /// <summary>Gap between columns in DIPs.</summary>
    public double ColumnSpacing { get => (double)GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    /// <summary>Sets rows (same as the property setter).</summary>
    public SkUiGrid SetRowDefinitions(RowDefinitionCollection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        RowDefinitions = value;
        return this;
    }

    private void OnRowDefinitionsChanged(RowDefinitionCollection value)
    {
        _rows.ItemSizeChanged -= OnDefinitionsChanged;
        _rows = value;
        _rows.ItemSizeChanged += OnDefinitionsChanged;
        OnDefinitionsChanged(this, EventArgs.Empty);
    }
    /// <summary>Sets columns (same as the property setter).</summary>
    public SkUiGrid SetColumnDefinitions(ColumnDefinitionCollection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ColumnDefinitions = value;
        return this;
    }

    private void OnColumnDefinitionsChanged(ColumnDefinitionCollection value)
    {
        _columns.ItemSizeChanged -= OnDefinitionsChanged;
        _columns = value;
        _columns.ItemSizeChanged += OnDefinitionsChanged;
        OnDefinitionsChanged(this, EventArgs.Empty);
    }
    /// <summary>Sets row spacing (same as the property setter).</summary>
    public SkUiGrid SetRowSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); RowSpacing = value; return this; }
    private void OnRowSpacingChanged(double value) { _rowSpacing = value; InvalidateMeasureOverride(); }
    /// <summary>Sets column spacing (same as the property setter).</summary>
    public SkUiGrid SetColumnSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); ColumnSpacing = value; return this; }
    private void OnColumnSpacingChanged(double value) { _columnSpacing = value; InvalidateMeasureOverride(); }

    IReadOnlyList<IGridRowDefinition> IGridLayout.RowDefinitions => _rowSnapshot ??= _rows.Cast<IGridRowDefinition>().ToArray();
    IReadOnlyList<IGridColumnDefinition> IGridLayout.ColumnDefinitions => _columnSnapshot ??= _columns.Cast<IGridColumnDefinition>().ToArray();
    /// <inheritdoc />
    private protected override bool AffectsChildLayout(string? propertyName) =>
        propertyName == Grid.RowProperty.PropertyName || propertyName == Grid.ColumnProperty.PropertyName
        || propertyName == Grid.RowSpanProperty.PropertyName || propertyName == Grid.ColumnSpanProperty.PropertyName;

    // MAUI Grid's per-view members, reading and writing the Grid.Row / Column / RowSpan / ColumnSpan attached properties.

    /// <summary>The zero-based row of <paramref name="view"/> (<c>Grid.Row</c>).</summary>
    public int GetRow(IView view) => Grid.GetRow(Bindable(view));
    /// <summary>The zero-based column of <paramref name="view"/> (<c>Grid.Column</c>).</summary>
    public int GetColumn(IView view) => Grid.GetColumn(Bindable(view));
    /// <summary>The rows <paramref name="view"/> spans (<c>Grid.RowSpan</c>).</summary>
    public int GetRowSpan(IView view) => Grid.GetRowSpan(Bindable(view));
    /// <summary>The columns <paramref name="view"/> spans (<c>Grid.ColumnSpan</c>).</summary>
    public int GetColumnSpan(IView view) => Grid.GetColumnSpan(Bindable(view));

    /// <summary>Sets the zero-based row of <paramref name="view"/> (<c>Grid.Row</c>).</summary>
    public SkUiGrid SetRow(IView view, int row) { Bindable(view).SetValue(Grid.RowProperty, row); return this; }
    /// <summary>Sets the zero-based column of <paramref name="view"/> (<c>Grid.Column</c>).</summary>
    public SkUiGrid SetColumn(IView view, int column) { Bindable(view).SetValue(Grid.ColumnProperty, column); return this; }
    /// <summary>Sets the rows <paramref name="view"/> spans (<c>Grid.RowSpan</c>, at least 1).</summary>
    public SkUiGrid SetRowSpan(IView view, int span) { Bindable(view).SetValue(Grid.RowSpanProperty, span); return this; }
    /// <summary>Sets the columns <paramref name="view"/> spans (<c>Grid.ColumnSpan</c>, at least 1).</summary>
    public SkUiGrid SetColumnSpan(IView view, int span) { Bindable(view).SetValue(Grid.ColumnSpanProperty, span); return this; }

    /// <summary>Appends a row definition (MAUI's <c>Grid.AddRowDefinition</c>).</summary>
    public SkUiGrid AddRowDefinition(RowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RowDefinitions.Add(definition);
        return this;
    }

    /// <summary>Appends a column definition (MAUI's <c>Grid.AddColumnDefinition</c>).</summary>
    public SkUiGrid AddColumnDefinition(ColumnDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ColumnDefinitions.Add(definition);
        return this;
    }

    private static BindableObject Bindable(IView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view as BindableObject ?? throw new ArgumentException($"SkiaUi grids place drawn (SkUi*) views, not {view.GetType().Name}.", nameof(view));
    }
    private void OnDefinitionsChanged(object? sender, EventArgs args)
    {
        _rowSnapshot = null;
        _columnSnapshot = null;
        UpdateDefinitionBindingContexts();
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        UpdateDefinitionBindingContexts();
    }

    // Definitions bind against the grid (<RowDefinition Height="{Binding …}"/>), as in MAUI's Grid; ones removed or left in
    // a replaced collection give the context back.
    private void UpdateDefinitionBindingContexts()
    {
        BindableObject[] current = [.. _rows, .. _columns];
        foreach (var previous in _boundDefinitions)
            if (Array.IndexOf(current, previous) < 0)
                ReleaseBindingContext(previous);
        foreach (var definition in current)
            InheritBindingContext(definition);
        _boundDefinitions = current;
    }

    private BindableObject[] _boundDefinitions = [];
    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));
}