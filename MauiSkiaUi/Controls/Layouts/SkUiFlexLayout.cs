using MauiSkiaUi.Flex;
using Microsoft.Maui.Layouts;

namespace MauiSkiaUi;

/// <summary>
/// A drawn flex layout with MAUI's <see cref="FlexLayout"/> API, laid out by MAUI's <see cref="FlexLayoutManager"/>.
/// Children use MAUI's attached properties (<c>FlexLayout.Grow</c>, <c>Shrink</c>, <c>Basis</c>, <c>Order</c>,
/// <c>AlignSelf</c>), also reachable through this type. MAUI's flex engine is internal, so a port of it
/// (<see cref="SkUiFlexItem"/>) computes the frames.
/// </summary>
public class SkUiFlexLayout : SkUiLayout, IFlexLayout
{
    private readonly FlexLayoutManager _manager;
    private readonly SkUiFlexItem _root = new();
    private Dictionary<ISkUiView, SkUiFlexItem> _items = [];
    private Dictionary<ISkUiView, SkUiFlexItem> _spareItems = [];
    private bool _itemsDirty;
    private bool _inMeasureMode;
    private FlexDirection _direction = FlexDirection.Row;
    private FlexWrap _wrap = FlexWrap.NoWrap;
    private FlexJustify _justifyContent = FlexJustify.Start;
    private FlexAlignItems _alignItems = FlexAlignItems.Stretch;
    private FlexAlignContent _alignContent = FlexAlignContent.Stretch;
    private FlexPosition _position = FlexPosition.Relative;

    /// <summary>Bindable main-axis direction.</summary>
    public static readonly BindableProperty DirectionProperty = BindableProperty.Create(
        nameof(Direction), typeof(FlexDirection), typeof(SkUiFlexLayout), FlexDirection.Row,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetDirection((FlexDirection)value));
    /// <summary>Bindable wrapping mode.</summary>
    public static readonly BindableProperty WrapProperty = BindableProperty.Create(
        nameof(Wrap), typeof(FlexWrap), typeof(SkUiFlexLayout), FlexWrap.NoWrap,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetWrap((FlexWrap)value));
    /// <summary>Bindable main-axis distribution of free space.</summary>
    public static readonly BindableProperty JustifyContentProperty = BindableProperty.Create(
        nameof(JustifyContent), typeof(FlexJustify), typeof(SkUiFlexLayout), FlexJustify.Start,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetJustifyContent((FlexJustify)value));
    /// <summary>Bindable cross-axis alignment of the children within a line.</summary>
    public static readonly BindableProperty AlignItemsProperty = BindableProperty.Create(
        nameof(AlignItems), typeof(FlexAlignItems), typeof(SkUiFlexLayout), FlexAlignItems.Stretch,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetAlignItems((FlexAlignItems)value));
    /// <summary>Bindable cross-axis distribution of the lines when wrapping.</summary>
    public static readonly BindableProperty AlignContentProperty = BindableProperty.Create(
        nameof(AlignContent), typeof(FlexAlignContent), typeof(SkUiFlexLayout), FlexAlignContent.Stretch,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetAlignContent((FlexAlignContent)value));
    /// <summary>Bindable <see cref="IFlexLayout.Position"/>; kept for MAUI API parity, it does not affect the layout (as in MAUI).</summary>
    public static readonly BindableProperty PositionProperty = BindableProperty.Create(
        nameof(Position), typeof(FlexPosition), typeof(SkUiFlexLayout), FlexPosition.Relative,
        propertyChanged: (view, _, value) => ((SkUiFlexLayout)view).SetPosition((FlexPosition)value));

    /// <summary>MAUI's attached <c>FlexLayout.Order</c> (visual order; stable for equal values).</summary>
    public static readonly BindableProperty OrderProperty = FlexLayout.OrderProperty;
    /// <summary>MAUI's attached <c>FlexLayout.Grow</c> (share of free main-axis space; ≥ 0).</summary>
    public static readonly BindableProperty GrowProperty = FlexLayout.GrowProperty;
    /// <summary>MAUI's attached <c>FlexLayout.Shrink</c> (share of the overflow to give up; ≥ 0, default 1).</summary>
    public static readonly BindableProperty ShrinkProperty = FlexLayout.ShrinkProperty;
    /// <summary>MAUI's attached <c>FlexLayout.AlignSelf</c> (overrides <see cref="AlignItems"/> for one child).</summary>
    public static readonly BindableProperty AlignSelfProperty = FlexLayout.AlignSelfProperty;
    /// <summary>MAUI's attached <c>FlexLayout.Basis</c> (initial main-axis size: auto, absolute or relative).</summary>
    public static readonly BindableProperty BasisProperty = FlexLayout.BasisProperty;

    /// <summary>Creates a flex layout with MAUI layout management.</summary>
    public SkUiFlexLayout() => _manager = new FlexLayoutManager(this);

    /// <summary>Main-axis direction; default <see cref="FlexDirection.Row"/>.</summary>
    public FlexDirection Direction { get => _direction; set => SetValue(DirectionProperty, value); }
    /// <summary>Whether children wrap onto new lines; default <see cref="FlexWrap.NoWrap"/>.</summary>
    public FlexWrap Wrap { get => _wrap; set => SetValue(WrapProperty, value); }
    /// <summary>Main-axis distribution of free space; default <see cref="FlexJustify.Start"/>.</summary>
    public FlexJustify JustifyContent { get => _justifyContent; set => SetValue(JustifyContentProperty, value); }
    /// <summary>Cross-axis alignment within a line; default <see cref="FlexAlignItems.Stretch"/>.</summary>
    public FlexAlignItems AlignItems { get => _alignItems; set => SetValue(AlignItemsProperty, value); }
    /// <summary>Distribution of wrapped lines on the cross axis; default <see cref="FlexAlignContent.Stretch"/>.</summary>
    public FlexAlignContent AlignContent { get => _alignContent; set => SetValue(AlignContentProperty, value); }
    /// <summary>MAUI API parity; does not affect the layout (as in MAUI).</summary>
    public FlexPosition Position { get => _position; set => SetValue(PositionProperty, value); }

    /// <summary>Sets <see cref="Direction"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetDirection(FlexDirection value) { _direction = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="Wrap"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetWrap(FlexWrap value) { _wrap = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="JustifyContent"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetJustifyContent(FlexJustify value) { _justifyContent = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="AlignItems"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetAlignItems(FlexAlignItems value) { _alignItems = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="AlignContent"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetAlignContent(FlexAlignContent value) { _alignContent = value; InvalidateMeasureOverride(); return this; }
    /// <summary>Sets <see cref="Position"/> without bindable write-back.</summary>
    public SkUiFlexLayout SetPosition(FlexPosition value) { _position = value; return this; }

    /// <summary>Gets a child's visual order.</summary>
    public static int GetOrder(BindableObject view) => FlexLayout.GetOrder(view);
    /// <summary>Sets a child's visual order.</summary>
    public static void SetOrder(BindableObject view, int value) => FlexLayout.SetOrder(view, value);
    /// <summary>Gets a child's grow factor.</summary>
    public static float GetGrow(BindableObject view) => FlexLayout.GetGrow(view);
    /// <summary>Sets a child's grow factor (≥ 0).</summary>
    public static void SetGrow(BindableObject view, float value) => FlexLayout.SetGrow(view, value);
    /// <summary>Gets a child's shrink factor.</summary>
    public static float GetShrink(BindableObject view) => FlexLayout.GetShrink(view);
    /// <summary>Sets a child's shrink factor (≥ 0).</summary>
    public static void SetShrink(BindableObject view, float value) => FlexLayout.SetShrink(view, value);
    /// <summary>Gets a child's cross-axis alignment override.</summary>
    public static FlexAlignSelf GetAlignSelf(BindableObject view) => FlexLayout.GetAlignSelf(view);
    /// <summary>Sets a child's cross-axis alignment override.</summary>
    public static void SetAlignSelf(BindableObject view, FlexAlignSelf value) => FlexLayout.SetAlignSelf(view, value);
    /// <summary>Gets a child's initial main-axis size.</summary>
    public static FlexBasis GetBasis(BindableObject view) => FlexLayout.GetBasis(view);
    /// <summary>Sets a child's initial main-axis size.</summary>
    public static void SetBasis(BindableObject view, FlexBasis value) => FlexLayout.SetBasis(view, value);

    int IFlexLayout.GetOrder(IView view) => FlexLayout.GetOrder((BindableObject)view);
    float IFlexLayout.GetGrow(IView view) => FlexLayout.GetGrow((BindableObject)view);
    float IFlexLayout.GetShrink(IView view) => FlexLayout.GetShrink((BindableObject)view);
    FlexAlignSelf IFlexLayout.GetAlignSelf(IView view) => FlexLayout.GetAlignSelf((BindableObject)view);
    FlexBasis IFlexLayout.GetBasis(IView view) => FlexLayout.GetBasis((BindableObject)view);

    /// <inheritdoc />
    public Rect GetFlexFrame(IView view) =>
        _items.TryGetValue((ISkUiView)view, out var item) ? item.GetFrame() : Rect.Zero;

    /// <summary>Runs the flex engine for the content area (padding already removed by <see cref="FlexLayoutManager"/>).</summary>
    public void Layout(double width, double height)
    {
        EnsureItems();
        _root.Direction = _direction;
        _root.Wrap = _wrap;
        _root.JustifyContent = _justifyContent;
        _root.AlignItems = _alignItems;
        _root.AlignContent = _alignContent;
        var children = Children;
        for (var index = 0; index < children.Count; index++)
            InitItemProperties(children[index], _items[children[index]]);

        // MAUI's FlexLayout also has an infinite-constraint "measure hack" (shrink 0, align-self Start), but in
        // 10.0.101 it runs before the item properties are refreshed, which overwrite it, so it has no effect.
        // The engine already skips shrinking and stretching on an axis of size 0 (unconstrained).
        _root.Width = !double.IsPositiveInfinity(width) ? (float)width : 0;
        _root.Height = !double.IsPositiveInfinity(height) ? (float)height : 0;
        _root.Layout(_inMeasureMode);
    }

    /// <inheritdoc />
    private protected override bool AffectsChildLayout(string? propertyName) =>
        propertyName == FlexLayout.OrderProperty.PropertyName || propertyName == FlexLayout.GrowProperty.PropertyName
        || propertyName == FlexLayout.ShrinkProperty.PropertyName || propertyName == FlexLayout.AlignSelfProperty.PropertyName
        || propertyName == FlexLayout.BasisProperty.PropertyName;

    /// <inheritdoc />
    private protected override void OnChildrenChanged() => _itemsDirty = true;

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        // The engine's self-sizing measures children only in measure mode; arrange reuses their desired sizes.
        _inMeasureMode = true;
        try
        {
            return _manager.Measure(widthConstraint, heightConstraint);
        }
        finally
        {
            _inMeasureMode = false;
        }
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => _manager.ArrangeChildren(new Rect(Point.Zero, size));

    /// <summary>Rebuilds the root's items in child order after children changed, reusing each child's item.</summary>
    private void EnsureItems()
    {
        if (!_itemsDirty && _items.Count == Children.Count)
            return;
        _itemsDirty = false;
        _root.Clear();
        foreach (var child in Children)
        {
            if (!_items.Remove(child, out var item))
                item = CreateItem(child);
            _spareItems[child] = item;
            _root.Add(item);
        }
        // Whatever is left belongs to removed children; drop it so they are not kept alive.
        _items.Clear();
        (_items, _spareItems) = (_spareItems, _items);
    }

    private SkUiFlexItem CreateItem(ISkUiView child)
    {
        var item = new SkUiFlexItem();
        item.SelfSizing = (SkUiFlexItem it, ref float w, ref float h, bool inMeasureMode) =>
        {
            Size request;
            if (inMeasureMode)
            {
                var constraints = it.GetConstraints();
                if (constraints.Width == 0)
                    constraints.Width = double.PositiveInfinity;
                if (constraints.Height == 0)
                    constraints.Height = double.PositiveInfinity;

                // dotnet/maui#27520: an item is never arranged larger than the container minus its own margins,
                // so measure against that; and once the engine has fixed the cross-axis size (stretch or an
                // explicit request), measure with exactly that. Only the cross axis is safe to clamp: the main
                // axis is still the pre-flex basis here.
                if (!double.IsPositiveInfinity(constraints.Width))
                    constraints.Width = Math.Max(0, constraints.Width - (it.MarginLeft + it.MarginRight));
                if (!double.IsPositiveInfinity(constraints.Height))
                    constraints.Height = Math.Max(0, constraints.Height - (it.MarginTop + it.MarginBottom));
                var mainAxisIsHorizontal = _direction is FlexDirection.Row or FlexDirection.RowReverse;
                if (!mainAxisIsHorizontal && !float.IsNaN(w) && w > 0 && w < constraints.Width)
                    constraints.Width = w;
                if (mainAxisIsHorizontal && !float.IsNaN(h) && h > 0 && h < constraints.Height)
                    constraints.Height = h;

                // As MAUI does for Image: measure unconstrained for the intrinsic size.
                if (child is SkUiImage)
                    constraints = new Size(double.PositiveInfinity, double.PositiveInfinity);

                request = child.Measure(constraints.Width, constraints.Height);
            }
            else
            {
                // Arrange pass: never measure. With an explicit size request the item already has it; NaN keeps it.
                request = child.DesiredSize;
            }
            w = !inMeasureMode && !float.IsNaN(it.Width) ? float.NaN : (float)request.Width;
            h = !inMeasureMode && !float.IsNaN(it.Height) ? float.NaN : (float)request.Height;
        };
        return item;
    }

    private static void InitItemProperties(ISkUiView child, SkUiFlexItem item)
    {
        var bindable = (BindableObject)child;
        item.Order = FlexLayout.GetOrder(bindable);
        item.Grow = FlexLayout.GetGrow(bindable);
        item.Shrink = FlexLayout.GetShrink(bindable);
        item.Basis = SkUiFlexBasis.From(FlexLayout.GetBasis(bindable));
        item.AlignSelf = FlexLayout.GetAlignSelf(bindable);
        var margin = child.Margin;
        item.MarginLeft = (float)margin.Left;
        item.MarginTop = (float)margin.Top;
        item.MarginRight = (float)margin.Right;
        item.MarginBottom = (float)margin.Bottom;
        var width = child.Width;
        item.Width = double.IsNaN(width) || width < 0 ? float.NaN : (float)width;
        var height = child.Height;
        item.Height = double.IsNaN(height) || height < 0 ? float.NaN : (float)height;
        item.IsVisible = child.Visibility != Visibility.Collapsed;
    }
}
