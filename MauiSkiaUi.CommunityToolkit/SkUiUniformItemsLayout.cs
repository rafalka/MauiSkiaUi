using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Layouts;
using CommunityToolkit.Maui.Layouts;

namespace MauiSkiaUi.CommunityToolkit;

/// <summary>
/// A drawn layout of equal cells with the Community Toolkit's <see cref="UniformItemsLayout"/> API, laid out by the
/// toolkit's <see cref="UniformItemsLayoutManager"/>: every cell is as tall as the first visible child and as wide as the
/// width divided by the columns that fit at the first child's width, at most <see cref="MaxColumns"/> columns and
/// <see cref="MaxRows"/> rows. Children past <see cref="MaxRows"/> × <see cref="MaxColumns"/> are arranged empty, so they
/// neither draw nor take input. Children must be drawn views.
/// </summary>
public class SkUiUniformItemsLayout : SkUiLayout, IUniformItemsLayout
{
    private int _maxRows = int.MaxValue;
    private int _maxColumns = int.MaxValue;
    private UniformItemsLayoutManager? _manager;
    private double _cellWidth;

    /// <summary>Bindable property for <see cref="MaxRows"/>.</summary>
    public static readonly BindableProperty MaxRowsProperty = BindableProperty.Create(
        nameof(MaxRows), typeof(int), typeof(SkUiUniformItemsLayout), int.MaxValue, validateValue: IsLimit,
        propertyChanged: (view, _, value) => ((SkUiUniformItemsLayout)view).OnMaxRowsChanged((int)value));

    /// <summary>Bindable property for <see cref="MaxColumns"/>.</summary>
    public static readonly BindableProperty MaxColumnsProperty = BindableProperty.Create(
        nameof(MaxColumns), typeof(int), typeof(SkUiUniformItemsLayout), int.MaxValue, validateValue: IsLimit,
        propertyChanged: (view, _, value) => ((SkUiUniformItemsLayout)view).OnMaxColumnsChanged((int)value));

    /// <summary>The most rows laid out (≥ 1); default <see cref="int.MaxValue"/>, no limit.</summary>
    public int MaxRows { get => (int)GetValue(MaxRowsProperty); set => SetValue(MaxRowsProperty, value); }

    /// <summary>The most columns laid out (≥ 1); default <see cref="int.MaxValue"/>, as many as the width takes.</summary>
    public int MaxColumns { get => (int)GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    int IUniformItemsLayout.MaxRows => _maxRows;
    int IUniformItemsLayout.MaxColumns => _maxColumns;

    /// <summary>Sets <see cref="MaxRows"/>; throws for a value below 1.</summary>
    public SkUiUniformItemsLayout SetMaxRows(int value)
    {
        ThrowIfNotLimit(value, nameof(value));
        MaxRows = value;
        return this;
    }

    /// <summary>Sets <see cref="MaxColumns"/>; throws for a value below 1.</summary>
    public SkUiUniformItemsLayout SetMaxColumns(int value)
    {
        ThrowIfNotLimit(value, nameof(value));
        MaxColumns = value;
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // The toolkit's manager measures the first visible child once and keeps its size for as long as it lives, so a
        // first child that changes size (new text, a font scale) would keep the old cells. This runs only when the
        // layout's measure is invalid (its children, a child's measure, an option or the constraint changed), so a new
        // manager per measure follows the first child without re-measuring anything that has not changed: children
        // cache their measure per constraint.
        return MeasureWithNewManager(widthConstraint, heightConstraint);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        if (_manager is null)
            MeasureWithNewManager(size.Width, size.Height);
        _manager!.ArrangeChildren(new Rect(Point.Zero, size));
        ArrangeChildrenPastTheLimit(size.Width);
    }

    private Size MeasureWithNewManager(double widthConstraint, double heightConstraint)
    {
        _manager = new UniformItemsLayoutManager(this);
        var size = _manager.Measure(widthConstraint, heightConstraint);
        // The manager has just measured the first visible child without limits: its width is the manager's cell width.
        // (Arranging measures each child again at its cell, so its desired size is read here, not after.)
        _cellWidth = FirstVisibleChild()?.DesiredSize.Width ?? 0;
        return size;
    }

    // The manager arranges the first rows × columns visible children and leaves the rest where they were last arranged.
    // They are arranged empty instead: nothing is drawn or hit at a zero size.
    private void ArrangeChildrenPastTheLimit(double width)
    {
        if (_maxRows == int.MaxValue)
            return;
        var visible = 0;
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Visible)
                visible++;
        }
        var capacity = (long)_maxRows * ColumnsArranged(visible, width - ((IPadding)this).Padding.HorizontalThickness);
        if (visible <= capacity)
            return;
        var index = 0;
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Visible)
                continue;
            if (index++ >= capacity)
                child.Arrange(Rect.Zero);
        }
    }

    // The manager's column count (GetColumnsCount is private): as many cells of the first child's width as fit, at least
    // one, at most MaxColumns and the visible children.
    private int ColumnsArranged(int visible, double width)
    {
        var columns = _cellWidth > 0 && double.IsFinite(width) ? Math.Min((int)(width / _cellWidth), visible) : visible;
        return Math.Clamp(columns, 1, _maxColumns);
    }

    private ISkUiView? FirstVisibleChild()
    {
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Visible)
                return child;
        }
        return null;
    }

    private void OnMaxRowsChanged(int value)
    {
        _maxRows = value;
        InvalidateMeasureOverride();
    }

    private void OnMaxColumnsChanged(int value)
    {
        _maxColumns = value;
        InvalidateMeasureOverride();
    }

    private static bool IsLimit(BindableObject bindable, object? value) => value is >= 1;

    private static void ThrowIfNotLimit(int value, string name)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(name, value, "The limit must be at least 1.");
    }
}
