using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>A single-child Skia composition host; GPU rendering is the standalone default.</summary>
[ContentProperty(nameof(Content))]
public class SkUiContentView : SkUiView
{
    private ISkUiView? _content;
    private Thickness _padding;

    /// <summary>Bindable inset around content.</summary>
    public static readonly BindableProperty PaddingProperty = BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(SkUiContentView), default(Thickness),
        propertyChanged: (view, _, value) => ((SkUiContentView)view).OnPaddingChanged((Thickness)value));
    /// <summary>Inset around the hosted content.</summary>
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    /// <summary>Sets padding (same as the property setter).</summary>
    public SkUiContentView SetPadding(Thickness value) { Padding = value; return this; }
    private void OnPaddingChanged(Thickness value) { _padding = value; InvalidateMeasureOverride(); }

    /// <summary>The bindable single-child content property.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(ISkUiView), typeof(SkUiContentView), null,
        validateValue: (bindable, value) =>
        {
            if (value is ISkUiView child && !ReferenceEquals(((SkUiContentView)bindable).Content, child))
                ((SkUiContentView)bindable).ValidateChild(child);
            return true;
        },
        propertyChanged: (bindable, _, newValue) => ((SkUiContentView)bindable).OnContentPropertyChanged((ISkUiView?)newValue));

    /// <summary>Creates a GPU-backed composition root when used in the MAUI visual tree.</summary>
    public SkUiContentView() => HwAccelerated = true;

    /// <summary>The handlerless child painted into this host's surface.</summary>
    public ISkUiView? Content
    {
        get => (ISkUiView?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Replaces content, cancelling the old capture.</summary>
    public SkUiContentView SetContent(ISkUiView? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null) ValidateChild(value);
        Content = value;
        return this;
    }

    private void OnContentPropertyChanged(ISkUiView? value)
    {
        var previous = _content;
        _content = value;
        if (previous is not null) DetachChild(previous);
        if (value is not null) AttachChild(value);
        OnContentChanged();
        InvalidateMeasureOverride();
    }

    /// <summary>Called after replacing the hosted child.</summary>
    protected virtual void OnContentChanged() { }

    /// <inheritdoc />
    internal override IEnumerable<ISkUiView> SkiaChildren { get { if (_content is not null) yield return _content; } }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var size = Content?.Measure(Math.Max(0, widthConstraint - _padding.HorizontalThickness), Math.Max(0, heightConstraint - _padding.VerticalThickness)) ?? Size.Zero;
        return new Size(size.Width + _padding.HorizontalThickness, size.Height + _padding.VerticalThickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => Content?.Arrange(new Rect(_padding.Left, _padding.Top,
        Math.Max(0, size.Width - _padding.HorizontalThickness), Math.Max(0, size.Height - _padding.VerticalThickness)));

}