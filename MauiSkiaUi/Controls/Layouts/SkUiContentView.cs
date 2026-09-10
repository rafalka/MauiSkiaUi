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
    private ISkUiView? content;
    private Thickness padding;

    /// <summary>Bindable inset around content.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiContentView), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiContentView)view).SetPadding((Thickness)value));
    /// <summary>Inset around the hosted content.</summary>
    public Thickness Padding { get => padding; set => SetValue(PaddingProperty, value); }
    /// <summary>Sets padding without bindable write-back.</summary>
    public SkUiContentView SetPadding(Thickness value) { padding = value; InvalidateMeasureOverride(); return this; }

    /// <summary>The bindable single-child content property.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiContentView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiContentView)bindable).Content, child))
                ((SkUiContentView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiContentView)bindable).SetContent((ISkUiView?)newValue));

    /// <summary>Creates a GPU-backed composition root when used in the MAUI visual tree.</summary>
    public SkUiContentView() => HwAccelerated = true;

    /// <summary>The handlerless child painted into this host's surface.</summary>
    public ISkUiView? Content
    {
        get => content;
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Replaces content without bindable write-back, cancelling the old capture.</summary>
    public SkUiContentView SetContent(ISkUiView? value)
    {
        if (ReferenceEquals(content, value)) return this;
        if (value is not null) ValidateChild(value);
        touchRouter.Cancel();
        var previous = content;
        content = value;
        if (previous is not null) DetachChild(previous);
        if (value is not null) AttachChild(value);
        OnContentChanged();
        InvalidateMeasureOverride();
        return this;
    }

    /// <summary>Called after replacing the hosted child.</summary>
    protected virtual void OnContentChanged() { }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (content is not null) yield return content; } }

    /// <summary>Cancels any active pointer capture in the hosted subtree.</summary>
    protected void CancelContentTouch() => touchRouter.Cancel();

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = Content?.Measure(Math.Max(0, widthConstraint - padding.HorizontalThickness), Math.Max(0, heightConstraint - padding.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + padding.HorizontalThickness, size.Height + padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => Content?.Arrange(new Rect(padding.Left, padding.Top,
        Math.Max(0, size.Width - padding.HorizontalThickness), Math.Max(0, size.Height - padding.VerticalThickness)));

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