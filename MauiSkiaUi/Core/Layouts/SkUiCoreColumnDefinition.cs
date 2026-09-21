namespace MauiSkiaUi.Core;

/// <summary>
/// Column width definition for <see cref="SkUiCoreGrid"/>, including optional min/max clamps
/// (Avalonia / WinUI style; not present on shipping MAUI <c>ColumnDefinition</c>).
/// </summary>
public sealed class SkUiCoreColumnDefinition
{
    private SkUiCoreGridLength _width = SkUiCoreGridLength.Star;
    private double _minWidth;
    private double _maxWidth = double.PositiveInfinity;

    /// <summary>Raised when Width, MinWidth, or MaxWidth changes.</summary>
    public event EventHandler? SizeChanged;

    /// <summary>Creates a star column.</summary>
    public SkUiCoreColumnDefinition() { }

    /// <summary>Creates a column with the given width.</summary>
    public SkUiCoreColumnDefinition(SkUiCoreGridLength width) => _width = width;

    /// <summary>Column width (Auto, absolute, or star).</summary>
    public SkUiCoreGridLength Width
    {
        get => _width;
        set => SetWidth(value);
    }

    /// <summary>Minimum column width in DIPs (default 0).</summary>
    public double MinWidth
    {
        get => _minWidth;
        set => SetMinWidth(value);
    }

    /// <summary>Maximum column width in DIPs (default unbounded).</summary>
    public double MaxWidth
    {
        get => _maxWidth;
        set => SetMaxWidth(value);
    }

    /// <summary>Sets the column width.</summary>
    public SkUiCoreColumnDefinition SetWidth(SkUiCoreGridLength value)
    {
        if (_width == value) return this;
        _width = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>Sets the minimum column width in DIPs.</summary>
    public SkUiCoreColumnDefinition SetMinWidth(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_minWidth.Equals(value)) return this;
        _minWidth = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>Sets the maximum column width in DIPs (use <see cref="double.PositiveInfinity"/> for unbounded).</summary>
    public SkUiCoreColumnDefinition SetMaxWidth(double value)
    {
        if (double.IsNaN(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_maxWidth.Equals(value)) return this;
        _maxWidth = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }
}
