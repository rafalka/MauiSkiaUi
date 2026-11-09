using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi;

/// <summary>
/// A drawn layout of equal tiles, for galleries and attachment strips on a page: as many columns of at most
/// <see cref="TileSize"/> as the width takes, tiles stretched to fill the row but never past <see cref="TileSize"/>, and a
/// column dropped when tiles would be smaller than <see cref="MinimumTileSize"/>. Tiles are <see cref="TileAspectRatio"/>
/// wide for each unit of height. With <see cref="MaximumRows"/>, the children past the limit are neither measured nor drawn
/// and the last tile shows <see cref="OverflowTemplate"/> over its child (bound to an <see cref="SkUiTileOverflow"/>: "+5").
/// A layout, not a virtualized list: galleries of many items belong in a collection view with a grid items layout.
/// Same engine as <see cref="Core.SkUiCoreTileLayout"/>.
/// </summary>
public class SkUiTileLayout : SkUiLayout
{
    /// <summary>The default <see cref="TileSize"/>, in DIPs.</summary>
    public const double DefaultTileSize = 120;

    private double _tileSize = DefaultTileSize;
    private double _minimumTileSize;
    private double _aspectRatio = 1;
    private double _columnSpacing;
    private double _rowSpacing;
    private int _maximumRows = -1;
    private SkUiTileState _state;
    private SkUiContentView? _overflowHost;
    private SkUiTileRenderSet _rendered;

    /// <summary>Bindable property for <see cref="TileSize"/>.</summary>
    public static readonly BindableProperty TileSizeProperty = BindableProperty.Create(nameof(TileSize), typeof(double), typeof(SkUiTileLayout),
        DefaultTileSize, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnOptionChanged(ref ((SkUiTileLayout)view)._tileSize, (double)value));

    /// <summary>Bindable property for <see cref="MinimumTileSize"/>.</summary>
    public static readonly BindableProperty MinimumTileSizeProperty = BindableProperty.Create(nameof(MinimumTileSize), typeof(double),
        typeof(SkUiTileLayout), 0d, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnOptionChanged(ref ((SkUiTileLayout)view)._minimumTileSize, (double)value));

    /// <summary>Bindable property for <see cref="TileAspectRatio"/>.</summary>
    public static readonly BindableProperty TileAspectRatioProperty = BindableProperty.Create(nameof(TileAspectRatio), typeof(double),
        typeof(SkUiTileLayout), 1d, validateValue: SkUiValidate.FinitePositive,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnOptionChanged(ref ((SkUiTileLayout)view)._aspectRatio, (double)value));

    /// <summary>Bindable property for <see cref="ColumnSpacing"/>.</summary>
    public static readonly BindableProperty ColumnSpacingProperty = BindableProperty.Create(nameof(ColumnSpacing), typeof(double),
        typeof(SkUiTileLayout), 0d, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnOptionChanged(ref ((SkUiTileLayout)view)._columnSpacing, (double)value));

    /// <summary>Bindable property for <see cref="RowSpacing"/>.</summary>
    public static readonly BindableProperty RowSpacingProperty = BindableProperty.Create(nameof(RowSpacing), typeof(double),
        typeof(SkUiTileLayout), 0d, validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnOptionChanged(ref ((SkUiTileLayout)view)._rowSpacing, (double)value));

    /// <summary>Bindable property for <see cref="MaximumRows"/>.</summary>
    public static readonly BindableProperty MaximumRowsProperty = BindableProperty.Create(nameof(MaximumRows), typeof(int), typeof(SkUiTileLayout),
        -1, validateValue: SkUiValidate.RowLimit,
        propertyChanged: (view, _, value) => ((SkUiTileLayout)view).OnMaximumRowsChanged((int)value));

    /// <summary>Bindable property for <see cref="OverflowTemplate"/>.</summary>
    public static readonly BindableProperty OverflowTemplateProperty = BindableProperty.Create(nameof(OverflowTemplate), typeof(DataTemplate),
        typeof(SkUiTileLayout), propertyChanged: (view, _, _) => ((SkUiTileLayout)view).OnOverflowTemplateChanged());

    /// <summary>Bindable property for <see cref="OverflowCommand"/>.</summary>
    public static readonly BindableProperty OverflowCommandProperty = BindableProperty.Create(nameof(OverflowCommand), typeof(ICommand), typeof(SkUiTileLayout));

    /// <summary>Bindable property for <see cref="OverflowCommandParameter"/>.</summary>
    public static readonly BindableProperty OverflowCommandParameterProperty = BindableProperty.Create(nameof(OverflowCommandParameter), typeof(object),
        typeof(SkUiTileLayout));

    /// <summary>The largest tile, in DIPs (default 120): the columns are the fewest whose tiles are at most this wide.</summary>
    public double TileSize { get => (double)GetValue(TileSizeProperty); set => SetValue(TileSizeProperty, value); }

    /// <summary>The smallest tile, in DIPs (default 0): a column is dropped while the tiles would be smaller.</summary>
    public double MinimumTileSize { get => (double)GetValue(MinimumTileSizeProperty); set => SetValue(MinimumTileSizeProperty, value); }

    /// <summary>A tile's width for each unit of its height (default 1, square; 16/9 for wide tiles).</summary>
    public double TileAspectRatio { get => (double)GetValue(TileAspectRatioProperty); set => SetValue(TileAspectRatioProperty, value); }

    /// <summary>Gap between columns, in DIPs.</summary>
    public double ColumnSpacing { get => (double)GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    /// <summary>Gap between rows, in DIPs.</summary>
    public double RowSpacing { get => (double)GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>The most rows shown (-1, the default: no limit). Children past them are neither measured nor drawn.</summary>
    public int MaximumRows { get => (int)GetValue(MaximumRowsProperty); set => SetValue(MaximumRowsProperty, value); }

    /// <summary>
    /// What the last tile shows over its child when children do not fit <see cref="MaximumRows"/>: a drawn view whose
    /// binding context is <see cref="Overflow"/> (a <see cref="DataTemplateSelector"/> chooses for it). None: the rows are
    /// only cut.
    /// </summary>
    public DataTemplate? OverflowTemplate { get => (DataTemplate?)GetValue(OverflowTemplateProperty); set => SetValue(OverflowTemplateProperty, value); }

    /// <summary>Executed when the overflow tile is tapped, with <see cref="OverflowCommandParameter"/> (default: <see cref="Overflow"/>).</summary>
    public ICommand? OverflowCommand { get => (ICommand?)GetValue(OverflowCommandProperty); set => SetValue(OverflowCommandProperty, value); }

    /// <summary>The parameter of <see cref="OverflowCommand"/>; <c>null</c>: <see cref="Overflow"/>.</summary>
    public object? OverflowCommandParameter { get => GetValue(OverflowCommandParameterProperty); set => SetValue(OverflowCommandParameterProperty, value); }

    /// <summary>The overflow tile's binding context: how many children did not fit.</summary>
    public SkUiTileOverflow Overflow { get; } = new();

    /// <summary>Raised when the overflow tile is tapped.</summary>
    public event EventHandler? OverflowTapped;

    /// <summary>Columns at the last layout.</summary>
    public int Columns => _state.Columns;

    /// <summary>Sets <see cref="TileSize"/>.</summary>
    public SkUiTileLayout SetTileSize(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); TileSize = value; return this; }

    /// <summary>Sets <see cref="MinimumTileSize"/>.</summary>
    public SkUiTileLayout SetMinimumTileSize(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); MinimumTileSize = value; return this; }

    /// <summary>Sets <see cref="TileAspectRatio"/>.</summary>
    public SkUiTileLayout SetTileAspectRatio(double value) { SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value)); TileAspectRatio = value; return this; }

    /// <summary>Sets <see cref="ColumnSpacing"/>.</summary>
    public SkUiTileLayout SetColumnSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); ColumnSpacing = value; return this; }

    /// <summary>Sets <see cref="RowSpacing"/>.</summary>
    public SkUiTileLayout SetRowSpacing(double value) { SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value)); RowSpacing = value; return this; }

    /// <summary>Sets <see cref="MaximumRows"/> (-1: no limit).</summary>
    public SkUiTileLayout SetMaximumRows(int value) { SkUiValidate.ThrowIfNotRowLimit(value, nameof(value)); MaximumRows = value; return this; }

    /// <summary>Sets <see cref="OverflowTemplate"/>.</summary>
    public SkUiTileLayout SetOverflowTemplate(DataTemplate? value) { OverflowTemplate = value; return this; }

    /// <summary>Sets <see cref="OverflowCommand"/>.</summary>
    public SkUiTileLayout SetOverflowCommand(ICommand? value) { OverflowCommand = value; return this; }

    private SkUiTileOptions Options => new(_tileSize, _minimumTileSize, _aspectRatio, _columnSpacing, _rowSpacing, _maximumRows, EffectivePadding);

    private void OnOptionChanged(ref double field, double value)
    {
        field = value;
        _state.Measured = false;
        InvalidateMeasureOverride();
    }

    private void OnMaximumRowsChanged(int value)
    {
        _maximumRows = value;
        _state.Measured = false;
        InvalidateMeasureOverride();
    }

    private void OnOverflowTemplateChanged()
    {
        if (_overflowHost is { } previous)
        {
            previous.Tapped -= OnOverflowHostTapped;
            _overflowHost = null;
            DetachChild(previous);
        }
        var template = OverflowTemplate;
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(Overflow, this);
        if (template?.CreateContent() is not { } content)
            return;
        if (content is not ISkUiView view)
            throw new InvalidOperationException($"{nameof(OverflowTemplate)} must create a drawn (SkUi*) view, not {content.GetType().Name}.");
        var host = new SkUiContentView { BindingContext = Overflow, Content = view };
        host.Tapped += OnOverflowHostTapped;
        ValidateChild(host);
        _overflowHost = host;
        AttachChild(host);
    }

    private void OnOverflowHostTapped(object? sender, SkUiTappedEventArgs e)
    {
        OverflowTapped?.Invoke(this, EventArgs.Empty);
        var parameter = OverflowCommandParameter ?? Overflow;
        if (OverflowCommand is { } command && command.CanExecute(parameter))
            command.Execute(parameter);
    }

    private bool ShowsOverflow => _overflowHost is not null && _state.HiddenCount > 0;

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren => _overflowHost is null ? Children : Children.Append(_overflowHost);

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiViewChildren(Children);
        var size = SkUiTileEngine.Measure(ref children, ref _state, widthConstraint, Options);
        AfterLayout();
        return size;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiViewChildren(Children);
        SkUiTileEngine.Arrange(ref children, ref _state, size, Options);
        AfterLayout();
        if (ShowsOverflow)
            ((IView)_overflowHost!).Arrange(_state.LastTile);
    }

    /// <summary>
    /// After a layout: the overflow count for bindings, the overflow tile measured for the last tile, and the drawn children
    /// sent again when the row limit now shows others.
    /// </summary>
    private void AfterLayout()
    {
        Overflow.SetHiddenCount(_state.HiddenCount);
        if (ShowsOverflow)
            ((IView)_overflowHost!).Measure(_state.TileWidth, _state.TileHeight);
        if (_rendered.Differs(_state.ShownEnd, ShowsOverflow))
            InvalidateRender(SkUiRenderDirty.Children);
    }

    /// <inheritdoc />
    /// <remarks>Only the children with a tile, then the overflow tile over them.</remarks>
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        var start = children.Count;
        if (!_state.Measured || _state.ShownEnd >= Children.Count)
            base.AddRenderChildren(children);
        else
        {
            foreach (var child in PaintOrder)
                if (child is ISkUiRenderable renderable && _state.IsShown(Children.IndexOf(child)))
                    children.Add(renderable);
        }
        if (ShowsOverflow)
            children.Add(_overflowHost!);
        _rendered.Update(this, children, start, _state.ShownEnd, ShowsOverflow, Children.Count);
    }
}
