namespace MauiSkiaUi;

/// <summary>
/// Hosts a real MAUI <see cref="VisualElement"/> (e.g. Entry, Editor, WebView) as a native overlay positioned
/// over this node's arranged bounds, instead of a Skia reimplementation (FR-16). Skia never draws the
/// wrapped control; the platform view renders and receives native input directly, so SkiaUi touch routing
/// never consumes hits over this region.
/// </summary>
/// <remarks>
/// v1 computes the overlay's root-relative position from this node's and each ancestor's <see cref="IView.Frame"/>
/// plus <see cref="VisualElement.TranslationX"/>/<see cref="VisualElement.TranslationY"/> (see
/// <see cref="ComputeRootRelativeFrame"/>); it does <b>not</b> account for <c>Rotation</c>, <c>Scale</c>, or
/// <c>Opacity</c> on this node or its ancestors between here and the standalone root, or snapshot-during-scroll.
/// Overlay attach/detach only has an effect on Android/iOS/Mac Catalyst/Windows builds; on the headless
/// <c>net10.0</c> target used for tests, the platform hooks are simply absent (no-ops), so Measure/Arrange/Touch
/// remain exercisable without a device.
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class SkUiMauiContentView : SkUiView
{
    private VisualElement? _content;
    /// <summary>Ancestor scrollers this overlay registered with while parented (for O(1) detach cleanup).</summary>
    private List<SkUiScrollView>? _registeredScrollers;

    /// <summary>Bindable hosted MAUI control.</summary>
    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(VisualElement), typeof(SkUiMauiContentView), null,
        propertyChanged: (view, _, value) => ((SkUiMauiContentView)view).SetContent((VisualElement?)value));

    /// <summary>The native MAUI control rendered over this node's arranged bounds.</summary>
    public VisualElement? Content { get => _content; set => SetValue(ContentProperty, value); }

    /// <summary>Replaces the hosted control without bindable write-back.</summary>
    public SkUiMauiContentView SetContent(VisualElement? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (value is not null && (value.Parent is not null || value.Handler is not null))
            throw new InvalidOperationException("A hosted MAUI control must be unparented and have no handler.");
        DetachOverlay();
        if (_content is not null) RemoveLogicalChild(_content);
        _content = value;
        if (_content is not null) AddLogicalChild(_content);
        InvalidateMeasureOverride();
        AttachOverlayIfPossible();
        return this;
    }

    /// <inheritdoc />
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) =>
        _content?.Measure(widthConstraint, heightConstraint) ?? Size.Zero;

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        _content?.Arrange(new Rect(Point.Zero, size));
        SyncOverlayBounds();
    }

    /// <summary>Never consumes hits: the native control receives input directly (see FR-15 boundary notes).</summary>
    public override bool Touch(SkUiTouchEvent touch) => false;

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null)
            UnregisterFromAncestorScrollers();
        base.OnParentSet();
        if (Parent is null) DetachOverlay();
        else
        {
            RegisterWithAncestorScrollers();
            AttachOverlayIfPossible();
        }
    }

    /// <summary>Called by the root handler when it connects/disconnects, to (re)try attaching the overlay.</summary>
    internal void NotifyRootAttached() => AttachOverlayIfPossible();

    /// <summary>Called by the root handler right before it disconnects.</summary>
    internal void NotifyRootDetached() => DetachOverlay();

    /// <summary>
    /// Root-relative arranged bounds: this node's <see cref="IView.Frame"/> plus every ancestor's Frame and
    /// translation offsets up to (excluding) the standalone root. Scroll offsets on ancestor
    /// <see cref="SkUiScrollView"/> nodes are subtracted so overlays track painted content. See the type-level
    /// remarks for the rotation/scale/opacity limit.
    /// </summary>
    internal Rect ComputeRootRelativeFrame()
    {
        double x = Frame.X + TranslationX, y = Frame.Y + TranslationY;
        for (var ancestor = Parent as SkUiView; ancestor is not null && ancestor.Handler is null; ancestor = ancestor.Parent as SkUiView)
        {
            x += ancestor.Frame.X + ancestor.TranslationX;
            y += ancestor.Frame.Y + ancestor.TranslationY;
            if (ancestor is SkUiScrollView scroll)
            {
                x -= scroll.ScrollX;
                y -= scroll.ScrollY;
            }
        }
        return new Rect(x, y, Frame.Width, Frame.Height);
    }

    /// <summary>Repositions the native overlay after an ancestor scroll offset change (no local rearrange).</summary>
    internal void NotifyAncestorScrollOffsetChanged() => SyncOverlayBounds();

    /// <summary>Registers with every ancestor scroller so offset sync stays O(overlays) instead of O(tree).</summary>
    private void RegisterWithAncestorScrollers()
    {
        UnregisterFromAncestorScrollers();
        for (var ancestor = Parent as SkUiView; ancestor is not null; ancestor = ancestor.Parent as SkUiView)
        {
            if (ancestor is not SkUiScrollView scroll)
                continue;
            scroll.RegisterOverlayDescendant(this);
            _registeredScrollers ??= [];
            _registeredScrollers.Add(scroll);
        }
    }

    /// <summary>Drops registrations from ancestor scrollers when this overlay leaves the tree.</summary>
    private void UnregisterFromAncestorScrollers()
    {
        if (_registeredScrollers is null || _registeredScrollers.Count == 0)
            return;
        foreach (var scroll in _registeredScrollers)
            scroll.UnregisterOverlayDescendant(this);
        _registeredScrollers.Clear();
    }

    partial void AttachOverlayIfPossible();
    partial void DetachOverlay();
    partial void SyncOverlayBounds();
}
