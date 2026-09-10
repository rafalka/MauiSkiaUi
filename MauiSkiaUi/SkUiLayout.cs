using System.Collections.ObjectModel;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A minimal overlay layout. Children share its slot and use MAUI margins and alignment.</summary>
[ContentProperty(nameof(Children))]
public class SkUiLayout : SkUiView
{
    private readonly SkUiTouchRouter touchRouter = new();

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
            var size = child.Measure(widthConstraint, heightConstraint);
            desired = new Size(Math.Max(desired.Width, size.Width), Math.Max(desired.Height, size.Height));
        }
        return desired;
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        foreach (var child in Children)
            child.Arrange(new Rect(Point.Zero, size));
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        foreach (var child in Children.OrderBy(child => child.ZIndex))
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
        foreach (var child in Children.OrderBy(child => child.ZIndex).Reverse())
            if (touchRouter.TryPress(child, touch))
                return true;
        return base.Touch(touch);
    }

    private sealed class ChildCollection(SkUiLayout owner) : Collection<ISkUiView>
    {
        protected override void InsertItem(int index, ISkUiView item)
        {
            owner.ValidateChild(item);
            base.InsertItem(index, item);
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
            owner.DetachChild(previous);
            owner.AttachChild(item);
        }

        protected override void RemoveItem(int index)
        {
            var previous = this[index];
            owner.touchRouter.Cancel();
            base.RemoveItem(index);
            owner.DetachChild(previous);
        }

        protected override void ClearItems()
        {
            owner.touchRouter.Cancel();
            var previous = this.ToArray();
            base.ClearItems();
            foreach (var child in previous)
                owner.DetachChild(child);
        }
    }
}