using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MauiSkiaUi;

/// <summary>
/// Hosts a real MAUI <see cref="VisualElement"/> (e.g. Entry, Editor, WebView) as a native overlay positioned
/// over this node's arranged bounds, instead of a Skia reimplementation (FR-16). Skia never draws the
/// wrapped control; the platform view renders and receives native input directly, so SkiaUi touch routing
/// never consumes hits over this region.
/// </summary>
/// <remarks>
/// v1 computes the overlay's root-relative position by summing ancestor <see cref="IView.Frame"/> offsets
/// (see <see cref="ComputeRootRelativeFrame"/>); it does not account for rotation/scale/opacity on ancestors
/// between this node and the standalone root, or snapshot-during-scroll. Overlay attach/detach only has an
/// effect on Android/iOS/Mac Catalyst/Windows builds; on the headless <c>net10.0</c> target used for tests,
/// the platform hooks are simply absent (no-ops), so Measure/Arrange/Touch remain exercisable without a device.
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class SkUiMauiContentView : SkUiView
{
    private VisualElement? content;

    /// <summary>Bindable hosted MAUI control.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(VisualElement), typeof(SkUiMauiContentView), null,
        propertyChanged: (view, _, value) => ((SkUiMauiContentView)view).SetContent((VisualElement?)value));

    /// <summary>The native MAUI control rendered over this node's arranged bounds.</summary>
    public VisualElement? Content { get => content; set => SetValue(ContentProperty, value); }

    /// <summary>Replaces the hosted control without bindable write-back.</summary>
    public SkUiMauiContentView SetContent(VisualElement? value)
    {
        if (ReferenceEquals(content, value)) return this;
        if (value is not null && (value.Parent is not null || value.Handler is not null))
            throw new InvalidOperationException("A hosted MAUI control must be unparented and have no handler.");
        DetachOverlay();
        if (content is not null) RemoveLogicalChild(content);
        content = value;
        if (content is not null) AddLogicalChild(content);
        InvalidateMeasureOverride();
        AttachOverlayIfPossible();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        content?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        content?.Arrange(new Rect(Point.Zero, size));
        SyncOverlayBounds();
    }

    /// <summary>Never consumes hits: the native control receives input directly (see FR-15 boundary notes).</summary>
    public override bool Touch(SkUiTouchEvent touch) => false;

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent is null) DetachOverlay();
        else AttachOverlayIfPossible();
    }

    /// <summary>Called by the root handler when it connects/disconnects, to (re)try attaching the overlay.</summary>
    internal void NotifyRootAttached() => AttachOverlayIfPossible();

    /// <summary>Called by the root handler right before it disconnects.</summary>
    internal void NotifyRootDetached() => DetachOverlay();

    /// <summary>
    /// Root-relative arranged bounds: this node's <see cref="IView.Frame"/> plus every ancestor's Frame
    /// offset up to (excluding) the standalone root. See the type-level remarks for the translation-only limit.
    /// </summary>
    internal Rect ComputeRootRelativeFrame()
    {
        double x = Frame.X, y = Frame.Y;
        for (var ancestor = Parent as SkUiView; ancestor is not null && ancestor.Handler is null; ancestor = ancestor.Parent as SkUiView)
        {
            x += ancestor.Frame.X;
            y += ancestor.Frame.Y;
        }
        return new Rect(x, y, Frame.Width, Frame.Height);
    }

    partial void AttachOverlayIfPossible();
    partial void DetachOverlay();
    partial void SyncOverlayBounds();
}
