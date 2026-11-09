using System.Windows.Input;
using MauiSkiaUi.Rendering;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core layout of equal tiles, for galleries and attachment strips: as many columns of at most <see cref="TileSize"/> as
/// the width takes, tiles stretched to fill the row but never past <see cref="TileSize"/>, and a column dropped when tiles
/// would be smaller than <see cref="MinimumTileSize"/>. Tiles are <see cref="TileAspectRatio"/> wide for each unit of
/// height. With <see cref="MaximumRows"/>, the children past the limit are neither measured nor drawn and the last tile
/// shows the node <see cref="OverflowTemplate"/> makes over its child. Same engine as <see cref="SkUiTileLayout"/>.
/// </summary>
public class SkUiCoreTileLayout : SkUiCorePanel
{
    private double _tileSize = SkUiTileLayout.DefaultTileSize;
    private double _minimumTileSize;
    private double _aspectRatio = 1;
    private double _columnSpacing;
    private double _rowSpacing;
    private int _maximumRows = -1;
    private Func<SkUiTileOverflow, SkUiCoreNode>? _overflowTemplate;
    private ICommand? _overflowCommand;
    private SkUiCoreContentView? _overflowHost;
    private SkUiTileState _state;
    private SkUiTileRenderSet _rendered;

    /// <inheritdoc cref="SkUiTileLayout.TileSize" />
    public double TileSize { get => _tileSize; set => SetTileSize(value); }

    /// <inheritdoc cref="SkUiTileLayout.MinimumTileSize" />
    public double MinimumTileSize { get => _minimumTileSize; set => SetMinimumTileSize(value); }

    /// <inheritdoc cref="SkUiTileLayout.TileAspectRatio" />
    public double TileAspectRatio { get => _aspectRatio; set => SetTileAspectRatio(value); }

    /// <inheritdoc cref="SkUiTileLayout.ColumnSpacing" />
    public double ColumnSpacing { get => _columnSpacing; set => SetColumnSpacing(value); }

    /// <inheritdoc cref="SkUiTileLayout.RowSpacing" />
    public double RowSpacing { get => _rowSpacing; set => SetRowSpacing(value); }

    /// <inheritdoc cref="SkUiTileLayout.MaximumRows" />
    public int MaximumRows { get => _maximumRows; set => SetMaximumRows(value); }

    /// <summary>
    /// Makes what the last tile shows over its child when children do not fit <see cref="MaximumRows"/>, called once with
    /// <see cref="Overflow"/> (whose <see cref="SkUiTileOverflow.HiddenCount"/> raises <c>PropertyChanged</c>). None: the rows
    /// are only cut.
    /// </summary>
    public Func<SkUiTileOverflow, SkUiCoreNode>? OverflowTemplate { get => _overflowTemplate; set => SetOverflowTemplate(value); }

    /// <summary>Executed with <see cref="Overflow"/> when the overflow tile is tapped.</summary>
    public ICommand? OverflowCommand { get => _overflowCommand; set => SetOverflowCommand(value); }

    /// <inheritdoc cref="SkUiTileLayout.Overflow" />
    public SkUiTileOverflow Overflow { get; } = new();

    /// <inheritdoc cref="SkUiTileLayout.OverflowTapped" />
    public event EventHandler? OverflowTapped;

    /// <inheritdoc cref="SkUiTileLayout.Columns" />
    public int Columns => _state.Columns;

    /// <summary>Sets <see cref="TileSize"/>.</summary>
    public SkUiCoreTileLayout SetTileSize(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return SetOption(ref _tileSize, value, nameof(TileSize));
    }

    /// <summary>Sets <see cref="MinimumTileSize"/>.</summary>
    public SkUiCoreTileLayout SetMinimumTileSize(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetOption(ref _minimumTileSize, value, nameof(MinimumTileSize));
    }

    /// <summary>Sets <see cref="TileAspectRatio"/>.</summary>
    public SkUiCoreTileLayout SetTileAspectRatio(double value)
    {
        SkUiValidate.ThrowIfNotFinitePositive(value, nameof(value));
        return SetOption(ref _aspectRatio, value, nameof(TileAspectRatio));
    }

    /// <summary>Sets <see cref="ColumnSpacing"/>.</summary>
    public SkUiCoreTileLayout SetColumnSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetOption(ref _columnSpacing, value, nameof(ColumnSpacing));
    }

    /// <summary>Sets <see cref="RowSpacing"/>.</summary>
    public SkUiCoreTileLayout SetRowSpacing(double value)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(value, nameof(value));
        return SetOption(ref _rowSpacing, value, nameof(RowSpacing));
    }

    /// <summary>Sets <see cref="MaximumRows"/> (-1: no limit).</summary>
    public SkUiCoreTileLayout SetMaximumRows(int value)
    {
        SkUiValidate.ThrowIfNotRowLimit(value, nameof(value));
        if (!SetProperty(ref _maximumRows, value, nameof(MaximumRows))) return this;
        _state.Measured = false;
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="OverflowTemplate"/>; the node it makes replaces the previous one.</summary>
    public SkUiCoreTileLayout SetOverflowTemplate(Func<SkUiTileOverflow, SkUiCoreNode>? value)
    {
        if (!SetProperty(ref _overflowTemplate, value, nameof(OverflowTemplate))) return this;
        if (_overflowHost is { } previous)
        {
            previous.Tapped -= OnOverflowHostTapped;
            _overflowHost = null;
            previous.AttachTo(null);
            SkUiDiagnostics.NotifyChildRemoved(this, previous, Children.Count);
        }
        if (value?.Invoke(Overflow) is { } content)
        {
            var host = new SkUiCoreContentView().SetContent(content);
            host.Tapped += OnOverflowHostTapped;
            host.AttachTo(this);
            _overflowHost = host;
            SkUiDiagnostics.NotifyChildAdded(this, host, Children.Count);
        }
        InvalidateRender(SkUiRenderDirty.Children);
        InvalidateMeasure();
        return this;
    }

    /// <summary>Sets <see cref="OverflowCommand"/>.</summary>
    public SkUiCoreTileLayout SetOverflowCommand(ICommand? value)
    {
        SetProperty(ref _overflowCommand, value, nameof(OverflowCommand));
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.Add" />
    public new SkUiCoreTileLayout Add(ISkUiCoreNode child)
    {
        base.Add(child);
        return this;
    }

    /// <inheritdoc cref="SkUiCorePanel.SetPadding" />
    public new SkUiCoreTileLayout SetPadding(Thickness value)
    {
        base.SetPadding(value);
        return this;
    }

    private SkUiCoreTileLayout SetOption(ref double field, double value, string name)
    {
        if (!SetProperty(ref field, value, name)) return this;
        _state.Measured = false;
        InvalidateMeasure();
        return this;
    }

    private void OnOverflowHostTapped(object? sender, SkUiTappedEventArgs e)
    {
        OverflowTapped?.Invoke(this, EventArgs.Empty);
        if (_overflowCommand is { } command && command.CanExecute(Overflow))
            command.Execute(Overflow);
    }

    private SkUiTileOptions Options => new(_tileSize, _minimumTileSize, _aspectRatio, _columnSpacing, _rowSpacing, _maximumRows, Padding);

    private bool ShowsOverflow => _overflowHost is not null && _state.HiddenCount > 0;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var children = new SkUiCoreNodeChildren(Children);
        var size = SkUiTileEngine.Measure(ref children, ref _state, widthConstraint, Options);
        AfterLayout();
        return size;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        var children = new SkUiCoreNodeChildren(Children);
        SkUiTileEngine.Arrange(ref children, ref _state, size, Options);
        AfterLayout();
        if (ShowsOverflow)
            _overflowHost!.Arrange(_state.LastTile);
    }

    /// <summary>See <see cref="SkUiTileLayout"/>'s: the count, the overflow tile measured, the drawn children sent again.</summary>
    private void AfterLayout()
    {
        Overflow.SetHiddenCount(_state.HiddenCount);
        if (ShowsOverflow)
            _overflowHost!.Measure(_state.TileWidth, _state.TileHeight);
        if (_rendered.Differs(_state.ShownEnd, ShowsOverflow))
            InvalidateRender(SkUiRenderDirty.Children);
    }

    /// <inheritdoc />
    internal override IReadOnlyList<IVisualTreeElement> VisualChildren =>
        _overflowHost is null ? base.VisualChildren : [.. base.VisualChildren, _overflowHost];

    /// <inheritdoc />
    /// <remarks>Only the children with a tile, then the overflow tile over them.</remarks>
    internal override void AddRenderChildren(List<ISkUiRenderable> children)
    {
        var start = children.Count;
        var count = _state.Measured ? Math.Min(_state.ShownEnd, Children.Count) : Children.Count;
        for (var index = 0; index < count; index++)
            if (Children[index] is ISkUiRenderable renderable)
                children.Add(renderable);
        if (ShowsOverflow)
            children.Add(_overflowHost!);
        _rendered.Update(this, children, start, count, ShowsOverflow, Children.Count);
    }
}
