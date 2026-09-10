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
        propertyChanged: (view, _, value) => ((SkUiGrid)view).SetRowDefinitions((RowDefinitionCollection)value));
    /// <summary>Bindable column definitions.</summary>
    public static readonly BindableProperty ColumnDefinitionsProperty = BindableProperty.Create(
        nameof(ColumnDefinitions), typeof(ColumnDefinitionCollection), typeof(SkUiGrid), null,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).SetColumnDefinitions((ColumnDefinitionCollection)value));
    /// <summary>Bindable gap between rows.</summary>
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(
        nameof(RowSpacing), typeof(double), typeof(SkUiGrid), 0d,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).SetRowSpacing((double)value));
    /// <summary>Bindable gap between columns.</summary>
    public static readonly BindableProperty ColumnSpacingProperty = BindableProperty.Create(
        nameof(ColumnSpacing), typeof(double), typeof(SkUiGrid), 0d,
        propertyChanged: (view, _, value) => ((SkUiGrid)view).SetColumnSpacing((double)value));

    /// <summary>Creates a grid with MAUI layout management.</summary>
    public SkUiGrid()
    {
        _manager = new GridLayoutManager(this);
        _rows.ItemSizeChanged += OnDefinitionsChanged;
        _columns.ItemSizeChanged += OnDefinitionsChanged;
    }

    /// <summary>Rows; an empty collection implies one star row.</summary>
    [System.ComponentModel.TypeConverter(typeof(RowDefinitionCollectionTypeConverter))]
    public RowDefinitionCollection RowDefinitions { get => _rows; set => SetValue(RowDefinitionsProperty, value); }
    /// <summary>Columns; an empty collection implies one star column.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColumnDefinitionCollectionTypeConverter))]
    public ColumnDefinitionCollection ColumnDefinitions { get => _columns; set => SetValue(ColumnDefinitionsProperty, value); }
    /// <summary>Gap between rows in DIPs.</summary>
    public double RowSpacing { get => _rowSpacing; set => SetValue(RowSpacingProperty, value); }
    /// <summary>Gap between columns in DIPs.</summary>
    public double ColumnSpacing { get => _columnSpacing; set => SetValue(ColumnSpacingProperty, value); }

    /// <summary>Sets rows without bindable write-back.</summary>
    public SkUiGrid SetRowDefinitions(RowDefinitionCollection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _rows.ItemSizeChanged -= OnDefinitionsChanged;
        _rows = value;
        _rows.ItemSizeChanged += OnDefinitionsChanged;
        OnDefinitionsChanged(this, EventArgs.Empty);
        return this;
    }
    /// <summary>Sets columns without bindable write-back.</summary>
    public SkUiGrid SetColumnDefinitions(ColumnDefinitionCollection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _columns.ItemSizeChanged -= OnDefinitionsChanged;
        _columns = value;
        _columns.ItemSizeChanged += OnDefinitionsChanged;
        OnDefinitionsChanged(this, EventArgs.Empty);
        return this;
    }
    /// <summary>Sets row spacing without bindable write-back.</summary>
    public SkUiGrid SetRowSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _rowSpacing = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets column spacing without bindable write-back.</summary>
    public SkUiGrid SetColumnSpacing(double value) { ArgumentOutOfRangeException.ThrowIfNegative(value); _columnSpacing = value; InvalidateMeasureOverride(); return this; }

    IReadOnlyList<IGridRowDefinition> IGridLayout.RowDefinitions => _rowSnapshot ??= _rows.Cast<IGridRowDefinition>().ToArray();
    IReadOnlyList<IGridColumnDefinition> IGridLayout.ColumnDefinitions => _columnSnapshot ??= _columns.Cast<IGridColumnDefinition>().ToArray();
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