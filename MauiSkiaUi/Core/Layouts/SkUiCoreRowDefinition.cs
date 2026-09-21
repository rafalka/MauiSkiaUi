namespace MauiSkiaUi.Core;

/// <summary>
/// Row height definition for <see cref="SkUiCoreGrid"/>, including optional min/max clamps
/// (Avalonia / WinUI style; not present on shipping MAUI <c>RowDefinition</c>).
/// </summary>
public sealed class SkUiCoreRowDefinition
{
    private SkUiCoreGridLength _height = SkUiCoreGridLength.Star;
    private double _minHeight;
    private double _maxHeight = double.PositiveInfinity;

    /// <summary>Raised when Height, MinHeight, or MaxHeight changes.</summary>
    public event EventHandler? SizeChanged;

    /// <summary>Creates a star row.</summary>
    public SkUiCoreRowDefinition() { }

    /// <summary>Creates a row with the given height.</summary>
    public SkUiCoreRowDefinition(SkUiCoreGridLength height) => _height = height;

    /// <summary>Row height (Auto, absolute, or star).</summary>
    public SkUiCoreGridLength Height
    {
        get => _height;
        set => SetHeight(value);
    }

    /// <summary>Minimum row height in DIPs (default 0).</summary>
    public double MinHeight
    {
        get => _minHeight;
        set => SetMinHeight(value);
    }

    /// <summary>Maximum row height in DIPs (default unbounded).</summary>
    public double MaxHeight
    {
        get => _maxHeight;
        set => SetMaxHeight(value);
    }

    /// <summary>Sets the row height.</summary>
    public SkUiCoreRowDefinition SetHeight(SkUiCoreGridLength value)
    {
        if (_height == value) return this;
        _height = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>Sets the minimum row height in DIPs.</summary>
    public SkUiCoreRowDefinition SetMinHeight(double value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_minHeight.Equals(value)) return this;
        _minHeight = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>Sets the maximum row height in DIPs (use <see cref="double.PositiveInfinity"/> for unbounded).</summary>
    public SkUiCoreRowDefinition SetMaxHeight(double value)
    {
        if (double.IsNaN(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (_maxHeight.Equals(value)) return this;
        _maxHeight = value;
        SizeChanged?.Invoke(this, EventArgs.Empty);
        return this;
    }
}
