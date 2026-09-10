using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A single-child Skia composition host; GPU rendering is the standalone default.</summary>
[ContentProperty(nameof(Content))]
public class SkUiContentView : SkUiView
{
    private readonly SkUiTouchRouter _touchRouter = new();
    private ISkUiView? _content;
    private Thickness _padding;

    /// <summary>Bindable inset around content.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiContentView), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiContentView)view).SetPadding((Thickness)value));
    /// <summary>Inset around the hosted content.</summary>
    public Thickness Padding { get => _padding; set => SetValue(PaddingProperty, value); }
    /// <summary>Sets padding without bindable write-back.</summary>
    public SkUiContentView SetPadding(Thickness value) { _padding = value; InvalidateMeasureOverride(); return this; }

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
        get => _content;
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Replaces content without bindable write-back, cancelling the old capture.</summary>
    public SkUiContentView SetContent(ISkUiView? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null) ValidateChild(value);
        _touchRouter.Cancel();
        var previous = _content;
        _content = value;
        if (previous is not null) DetachChild(previous);
        if (value is not null) AttachChild(value);
        OnContentChanged();
        InvalidateMeasureOverride();
        return this;
    }

    /// <summary>Called after replacing the hosted child.</summary>
    protected virtual void OnContentChanged() { }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (_content is not null) yield return _content; } }

    /// <summary>Cancels any active pointer capture in the hosted subtree.</summary>
    protected void CancelContentTouch() => _touchRouter.Cancel();

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = Content?.Measure(Math.Max(0, widthConstraint - _padding.HorizontalThickness), Math.Max(0, heightConstraint - _padding.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => Content?.Arrange(new Rect(_padding.Left, _padding.Top,
        Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));

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
            _touchRouter.Cancel();
            return IsVisible && !InputTransparent && !IsEnabled;
        }
        if (_touchRouter.DeliverCaptured(touch, out var handled))
            return handled;
        if (Content is { } child && _touchRouter.TryPress(child, touch))
            return true;
        return base.Touch(touch);
    }
}