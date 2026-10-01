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
    public SkUiGrid SetRowSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); RowSpacing = value; return this; }
    private void OnRowSpacingChanged(double value) { _rowSpacing = value; InvalidateMeasureOverride(); }
    /// <summary>Sets column spacing (same as the property setter).</summary>
    public SkUiGrid SetColumnSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); ColumnSpacing = value; return this; }
    private void OnColumnSpacingChanged(double value) { _columnSpacing = value; InvalidateMeasureOverride(); }

    IReadOnlyList<IGridRowDefinition> IGridLayout.RowDefinitions => _rowSnapshot ??= _rows.Cast<IGridRowDefinition>().ToArray();
    IReadOnlyList<IGridColumnDefinition> IGridLayout.ColumnDefinitions => _columnSnapshot ??= _columns.Cast<IGridColumnDefinition>().ToArray();
    /// <inheritdoc />
    private protected override bool AffectsChildLayout(string? propertyName) =>
        propertyName == Grid.RowProperty.PropertyName || propertyName == Grid.ColumnProperty.PropertyName
        || propertyName == Grid.RowSpanProperty.PropertyName || propertyName == Grid.ColumnSpanProperty.PropertyName;

    int IGridLayout.GetRow(IView view) => Grid.GetRow((BindableObject)view);
    int IGridLayout.GetColumn(IView view) => Grid.GetColumn((BindableObject)view);
    int IGridLayout.GetRowSpan(IView view) => Grid.GetRowSpan((BindableObject)view);
    int IGridLayout.GetColumnSpan(IView view) => Grid.GetColumnSpan((BindableObject)view);
    private void OnDefinitionsChanged(object? sender, EventArgs args)
    {
        _rowSnapshot = null;
        _columnSnapshot = null;
        InvalidateMeasureOverride();
    }
    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _manager.Measure(widthConstraint, heightConstraint);
    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));
}