using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using ILayout = Microsoft.Maui.ILayout;

namespace MauiSkiaUi;

/// <summary>A minimal overlay layout. Children share its slot and use MAUI margins and alignment.</summary>
[ContentProperty(nameof(Children))]
public class SkUiLayout : SkUiView, ILayout
{
    private readonly SkUiTouchRouter touchRouter = new();
    private Thickness padding;
    private ISkUiView[]? paintOrder;
    private ISkUiView[] PaintOrder => paintOrder ??= Children.OrderBy(child => child.ZIndex).ToArray();

    /// <summary>Bindable space inside the layout.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(
        nameof(Padding), typeof(Thickness), typeof(SkUiLayout), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiLayout)view).SetPadding((Thickness)value));

    /// <summary>Space inside the layout in DIPs.</summary>
    public Thickness Padding { get => padding; set => SetValue(PaddingProperty, value); }

    /// <summary>Sets padding without writing back to the bindable property.</summary>
    public SkUiLayout SetPadding(Thickness value)
    {
        if (padding == value) return this;
        padding = value;
        InvalidateMeasureOverride();
        return this;
    }

    bool ILayout.ClipsToBounds => true;
    bool ISafeAreaView.IgnoreSafeArea => true;
    Size ILayout.CrossPlatformMeasure(double widthConstraint, double heightConstraint) => MeasureContent(widthConstraint, heightConstraint);
    Size ILayout.CrossPlatformArrange(Rect bounds) { ArrangeContent(bounds.Size); return bounds.Size; }
    int ICollection<IView>.Count => Children.Count;
    bool ICollection<IView>.IsReadOnly => false;
    IView IList<IView>.this[int index] { get => Children[index]; set => Children[index] = RequireSkia(value); }
    void ICollection<IView>.Add(IView item) => Children.Add(RequireSkia(item));
    void ICollection<IView>.Clear() => Children.Clear();
    bool ICollection<IView>.Contains(IView item) => item is ISkUiView child && Children.Contains(child);
    void ICollection<IView>.CopyTo(IView[] array, int index) { foreach (var child in Children) array[index++] = child; }
    bool ICollection<IView>.Remove(IView item) => item is ISkUiView child && Children.Remove(child);
    int IList<IView>.IndexOf(IView item) => item is ISkUiView child ? Children.IndexOf(child) : -1;
    void IList<IView>.Insert(int index, IView item) => Children.Insert(index, RequireSkia(item));
    void IList<IView>.RemoveAt(int index) => Children.RemoveAt(index);
    IEnumerator<IView> IEnumerable<IView>.GetEnumerator() => Children.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Children.GetEnumerator();
    private static ISkUiView RequireSkia(IView view) => view as ISkUiView ?? throw new ArgumentException("Only SkiaUi children are supported.", nameof(view));

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ZIndex)) paintOrder = null;
        // Children carry standard MAUI Grid.Row/Column/RowSpan/ColumnSpan attached values (see SkUiGrid), but
        // Grid's own propertyChanged callback never fires our invalidation because Parent is SkUiGrid, not
        // Microsoft.Maui.Controls.Grid. Compare against the BindableProperty's own PropertyName (not a literal
        // string) so this keeps working if MAUI ever renames those attached properties.
        if (args.PropertyName == Grid.RowProperty.PropertyName || args.PropertyName == Grid.ColumnProperty.PropertyName
            || args.PropertyName == Grid.RowSpanProperty.PropertyName || args.PropertyName == Grid.ColumnSpanProperty.PropertyName)
            InvalidateMeasureOverride();
    }

    /// <summary>Creates a GPU-default layout with an owned child collection.</summary>
    public SkUiLayout()
    {
        HwAccelerated = true;
        Children = new ChildCollection(this);
    }

    /// <summary>Children in insertion order; ZIndex determines paint and hit-test order.</summary>
    public IList<ISkUiView> Children { get; }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var desired = Size.Zero;
        foreach (var child in Children)
        {
            var size = child.Measure(Math.Max(0, widthConstraint - padding.HorizontalThickness), Math.Max(0, heightConstraint - padding.VerticalThickness));
            desired = new Size(Math.Max(desired.Width, size.Width), Math.Max(desired.Height, size.Height));
        }
        return new Size(desired.Width + padding.HorizontalThickness, desired.Height + padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(padding.Left, padding.Top, Math.Max(0, size.Width - padding.HorizontalThickness), Math.Max(0, size.Height - padding.VerticalThickness)));
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        foreach (var child in PaintOrder)
            PaintChild(child, canvas);
    }

    /// <inheritdoc />
    public override bool Touch(SkUiTouchEvent touch)
    {
        if (InputTransparent || !IsVisible || !IsEnabled)
        {
            touchRouter.Cancel();
            return IsVisible && !InputTransparent && !IsEnabled;
        }
        if (touchRouter.DeliverCaptured(touch, out var handled))
            return handled;
        var order = PaintOrder;
        for (var index = order.Length - 1; index >= 0; index--)
            if (touchRouter.TryPress(order[index], touch))
                return true;
        return base.Touch(touch);
    }

    private sealed class ChildCollection(SkUiLayout owner) : Collection<ISkUiView>
    {
        protected override void InsertItem(int index, ISkUiView item)
        {
            owner.ValidateChild(item);
            base.InsertItem(index, item);
            owner.paintOrder = null;
            ((Element)item).PropertyChanged += owner.OnChildPropertyChanged;
            owner.AttachChild(item);
        }

        protected override void SetItem(int index, ISkUiView item)
        {
            if (ReferenceEquals(this[index], item))
                return;
            owner.ValidateChild(item);
            var previous = this[index];
            owner.touchRouter.Cancel();
            base.SetItem(index, item);
            owner.paintOrder = null;
            ((Element)previous).PropertyChanged -= owner.OnChildPropertyChanged;
            ((Element)item).PropertyChanged += owner.OnChildPropertyChanged;
            owner.DetachChild(previous);
            owner.AttachChild(item);
        }

        protected override void RemoveItem(int index)
        {
            var previous = this[index];
            owner.touchRouter.Cancel();
            base.RemoveItem(index);
            owner.paintOrder = null;
            ((Element)previous).PropertyChanged -= owner.OnChildPropertyChanged;
            owner.DetachChild(previous);
        }

        protected override void ClearItems()
        {
            owner.touchRouter.Cancel();
            var previous = this.ToArray();
            base.ClearItems();
            owner.paintOrder = null;
            foreach (var child in previous)
            {
                ((Element)child).PropertyChanged -= owner.OnChildPropertyChanged;
                owner.DetachChild(child);
            }
        }
    }
}