using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A single-child Skia composition host; GPU rendering is the standalone default.</summary>
[ContentProperty(nameof(Content))]
public class SkUiContentView : SkUiView
{
    private readonly SkUiTouchRouter touchRouter = new();

    /// <summary>The bindable single-child content property.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiContentView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiContentView)bindable).Content, child))
                ((SkUiContentView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var host = (SkUiContentView)bindable;
            host.touchRouter.Cancel();
            if (oldValue is ISkUiView previous)
                host.DetachChild(previous);
            if (newValue is ISkUiView current)
                host.AttachChild(current);
        });

    /// <summary>Creates a GPU-backed composition root when used in the MAUI visual tree.</summary>
    public SkUiContentView() => HwAccelerated = true;

    /// <summary>The handlerless child painted into this host's surface.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        Content?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => Content?.Arrange(new Rect(Point.Zero, size));

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (Content is { } child)
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
        if (Content is { } child && touchRouter.TryPress(child, touch))
            return true;
        return base.Touch(touch);
    }
}