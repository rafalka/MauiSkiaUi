using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>How a hosted native control follows scrolling (<see cref="SkUiMauiContentView.ScrollMode"/>).</summary>
public enum SkUiOverlayScrollMode
{
    /// <summary>Platform default: <see cref="Snapshot"/> on Android and Windows, <see cref="Live"/> on iOS / Mac Catalyst.</summary>
    Auto,
    /// <summary>
    /// While an ancestor scroller moves, the native view is hidden and a bitmap of it is drawn instead, so it moves in
    /// sync with the drawn content (also during render-thread flings while the UI thread is busy). Restored after the
    /// motion settles (<see cref="SkUiMauiContentView.SnapshotRestoreDelay"/>). A focused control stays live.
    /// </summary>
    Snapshot,
    /// <summary>The native view is repositioned on every offset change (it may trail fast drawn motion by a frame or two).</summary>
    Live
}

/// <summary>
/// Where the platform placed a hosted native view (<see cref="SkUiMauiContentView.GetNativePlacement"/>), in
/// root-relative DIPs: its <paramref name="Frame"/>, the <paramref name="Visible"/> part its clip wrapper shows, and
/// whether it <paramref name="IsShown"/> (not hidden for a snapshot or an invisible ancestor, not clipped away).
/// </summary>
internal readonly record struct SkUiOverlayPlacement(Rect Frame, Rect Visible, bool IsShown);

/// <summary>
/// Hosts a real MAUI <see cref="VisualElement"/> (e.g. Entry, Editor, WebView) as a native overlay positioned
/// over this node's arranged bounds, instead of a Skia reimplementation (FR-16). The platform view renders and
/// receives native input directly, so SkiaUi touch routing never consumes hits over this region. The overlay is
/// clipped to the viewports of ancestor scrollers (and ancestors with <see cref="SkUiView.ClipToBounds"/>), so it
/// never draws or takes touches outside them; while scrolling it can be frozen as a snapshot
/// (<see cref="ScrollMode"/>).
/// </summary>
/// <remarks>
/// v1 computes the overlay's root-relative position from this node's and each ancestor's <see cref="IView.Frame"/>
/// plus <see cref="VisualElement.TranslationX"/>/<see cref="VisualElement.TranslationY"/> (see
/// <see cref="ComputeRootRelativeFrame"/>); it does <b>not</b> account for <c>Rotation</c>, <c>Scale</c>, or
/// <c>Opacity</c> on this node or its ancestors between here and the standalone root.
/// Overlay attach/detach only has an effect on Android/iOS/Mac Catalyst/Windows builds; on the headless
/// <c>net10.0</c> target used for tests, the platform hooks are simply absent (no-ops), so Measure/Arrange/hit-testing
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
        validateValue: (bindable, value) => IsHostable((SkUiMauiContentView)bindable, (VisualElement?)value),
        propertyChanged: (view, _, value) => ((SkUiMauiContentView)view).OnContentChanged((VisualElement?)value));

    /// <summary>The native MAUI control rendered over this node's arranged bounds.</summary>
    public VisualElement? Content { get => (VisualElement?)GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>Bindable <see cref="ScrollMode"/>.</summary>
    public static readonly BindableProperty ScrollModeProperty = BindableProperty.Create(
        nameof(ScrollMode), typeof(SkUiOverlayScrollMode), typeof(SkUiMauiContentView), SkUiOverlayScrollMode.Auto,
        propertyChanged: (view, _, _) => ((SkUiMauiContentView)view).OnScrollModeChanged());

    /// <summary>How the native control follows ancestor scrolling (default <see cref="SkUiOverlayScrollMode.Auto"/>).</summary>
    public SkUiOverlayScrollMode ScrollMode { get => (SkUiOverlayScrollMode)GetValue(ScrollModeProperty); set => SetValue(ScrollModeProperty, value); }

    /// <summary>Delay after scrolling stops before a snapshot is replaced by the live native view again (default 150 ms).</summary>
    public static TimeSpan SnapshotRestoreDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Diagnostics: outline overlays while their snapshot is shown.</summary>
    public static bool HighlightSnapshots { get; set; }

    /// <summary>Whether a snapshot is currently drawn in place of the hidden native view.</summary>
    public bool IsShowingSnapshot => _snapshot is not null;

    /// <summary>Whether snapshots are used while scrolling (resolves <see cref="SkUiOverlayScrollMode.Auto"/> per platform).</summary>
    public bool UsesSnapshotWhileScrolling => ScrollMode switch
    {
        SkUiOverlayScrollMode.Snapshot => true,
        SkUiOverlayScrollMode.Live => false,
        _ => OperatingSystem.IsAndroid() || OperatingSystem.IsWindows()
    };

    private int _movingScrollers;
    private SKImage? _snapshot;
    private bool _watchingShown;
    private bool _notShown; // this view or a drawn ancestor is invisible (or the tree has no live surface)
    private bool _capturing;
    // Bumped whenever an in-flight capture becomes stale (restore, mode change, reset): its completion is ignored.
    private int _captureGeneration;
    private IDisposable? _restoreTimer;

    /// <summary>Test hook: replaces platform capture (called with the completion callback; return false when not started).</summary>
    internal static Func<SkUiMauiContentView, Action<SKImage?>, bool>? CaptureOverride { get; set; }

    /// <summary>An ancestor scroller started or stopped moving (drag, fling, animated scroll).</summary>
    internal void NotifyAncestorScrollMotion(bool moving)
    {
        _movingScrollers = Math.Max(0, _movingScrollers + (moving ? 1 : -1));
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"overlay {TraceName} ancestor moving={moving} -> movingScrollers={_movingScrollers} snapshot={_snapshot is not null} capturing={_capturing}");
        if (_movingScrollers > 0)
        {
            _restoreTimer?.Dispose();
            _restoreTimer = null;
            BeginSnapshot();
            return;
        }
        _restoreTimer?.Dispose();
        _restoreTimer = SkUiGestureSettings.Schedule(SnapshotRestoreDelay, EndSnapshot);
        if (_restoreTimer is null)
            EndSnapshot(); // no timer source (headless): restore at once
    }

    private void BeginSnapshot()
    {
        if (_snapshot is not null || _capturing || !UsesSnapshotWhileScrolling || _content is null or { IsFocused: true })
            return;
        _capturing = true;
        var generation = ++_captureGeneration;
        var started = false;
        void Done(SKImage? image)
        {
            if (generation != _captureGeneration)
                return; // superseded: a newer capture owns _capturing
            _capturing = false;
            if (SkUiDiagnostics.TraceOn)
                SkUiDiagnostics.Write($"overlay {TraceName} captured image={image is not null} movingScrollers={_movingScrollers} -> {(image is null || _movingScrollers == 0 ? "stay live" : "show snapshot")}");
            // Focus can arrive while an async capture (Windows) is in flight: a focused control stays live.
            if (image is null || _movingScrollers == 0 || _content is null || _content.IsFocused || !UsesSnapshotWhileScrolling)
            {
                SyncOverlayBounds();
                return;
            }
            _snapshot = image;
            ApplyNativeHidden();
            InvalidatePaint();
            OnPropertyChanged(nameof(IsShowingSnapshot));
        }
        if (CaptureOverride is { } capture)
            started = capture(this, Done);
        else
            StartCapture(Done, ref started);
        if (!started && generation == _captureGeneration)
            _capturing = false;
    }

    private void EndSnapshot()
    {
        _restoreTimer = null;
        if (SkUiDiagnostics.TraceOn)
            SkUiDiagnostics.Write($"overlay {TraceName} restore timer movingScrollers={_movingScrollers} snapshot={_snapshot is not null} -> {(_movingScrollers > 0 ? "keep snapshot" : "restore live")}");
        if (_movingScrollers > 0)
            return;
        RestoreLive();
    }

    private string TraceName => _content is null ? GetHashCode().ToString() : $"{_content.GetType().Name}#{_content.GetHashCode() % 10000}";

    /// <summary>Drops the snapshot (and any capture in flight) and shows the live native view again.</summary>
    private void RestoreLive()
    {
        _captureGeneration++;
        _capturing = false;
        if (_snapshot is null)
            return;
        // Never dispose: the retained picture on the render thread may still reference the image (GC frees it).
        _snapshot = null;
        SyncOverlayBounds();
        ApplyNativeHidden();
        InvalidatePaint();
        OnPropertyChanged(nameof(IsShowingSnapshot));
    }

    /// <summary>
    /// A focused control stays live: focusing it while its snapshot is shown (e.g. right after a scroll, before the
    /// restore delay) brings the native view back at once. Otherwise the user would type into a hidden field, and on
    /// iOS a text field that becomes first responder while hidden stays retained by UIKit after its page closes.
    /// </summary>
    private void OnContentFocused(object? sender, FocusEventArgs e)
    {
        _restoreTimer?.Dispose();
        _restoreTimer = null;
        RestoreLive();
    }

    private void OnScrollModeChanged()
    {
        // The moving-scroller count keeps tracking ancestors in every mode; only the snapshot follows the mode.
        if (!UsesSnapshotWhileScrolling)
        {
            _restoreTimer?.Dispose();
            _restoreTimer = null;
            RestoreLive();
        }
        else if (_movingScrollers > 0)
        {
            BeginSnapshot();
        }
    }

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas)
    {
        base.OnPaintContent(canvas);
        if (_snapshot is not { } image)
            return;
        var rect = new SKRect(0, 0, (float)Width, (float)Height);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, rect, new SKSamplingOptions(SKFilterMode.Linear), paint);
        if (HighlightSnapshots)
        {
            using var outline = new SKPaint { Color = new SKColor(0xFF, 0x3B, 0x30), IsStroke = true, StrokeWidth = 3, IsAntialias = true };
            canvas.DrawRect(SKRect.Inflate(rect, -1.5f, -1.5f), outline);
        }
    }

    /// <summary>Replaces the hosted control (same as the property setter).</summary>
    public SkUiMauiContentView SetContent(VisualElement? value)
    {
        if (ReferenceEquals(_content, value)) return this;
        if (!IsHostable(this, value))
            throw new InvalidOperationException("A hosted MAUI control must be unparented and have no handler.");
        Content = value;
        return this;
    }

    /// <summary>A hosted control must be unparented and have no handler (the current content always passes).</summary>
    private static bool IsHostable(SkUiMauiContentView host, VisualElement? value) =>
        value is null || ReferenceEquals(host._content, value) || (value.Parent is null && value.Handler is null);

    private void OnContentChanged(VisualElement? value)
    {
        DetachOverlay();
        if (_content is not null)
        {
            _content.Focused -= OnContentFocused;
            RemoveLogicalChild(_content);
        }
        _content = value;
        if (_content is not null)
        {
            _content.Focused += OnContentFocused;
            AddLogicalChild(_content);
        }
        InvalidateMeasureOverride();
        AttachOverlayIfPossible();
        // A snapshot shows the old control: drop it, and capture the new one if a scroller still moves.
        RestoreLive();
        if (_movingScrollers > 0)
            BeginSnapshot();
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

    /// <inheritdoc />
    internal override void NotifyMoved()
    {
        SyncOverlayBounds();
        base.NotifyMoved();
    }

    /// <inheritdoc />
    protected override void OnParentSet()
    {
        if (Parent is null)
        {
            UnregisterFromAncestorScrollers();
            ResetSnapshot();
        }
        base.OnParentSet();
        WatchShown(Parent is not null);
        if (Parent is null) DetachOverlay();
        else
        {
            RegisterWithAncestorScrollers();
            AttachOverlayIfPossible();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Runs on every descendant when any ancestor is (re)parented, so an overlay added to a stack before that stack
    /// is placed in a scroller (typical for XAML and code built bottom-up) still registers with its scrollers.
    /// </remarks>
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        if (Parent is null)
            return;
        RegisterWithAncestorScrollers();
        AttachOverlayIfPossible();
        SyncOverlayBounds();
    }

    /// <summary>Called by the root handler when it connects/disconnects, to (re)try attaching the overlay.</summary>
    internal void NotifyRootAttached() => AttachOverlayIfPossible();

    /// <summary>Called by the root handler right before it disconnects.</summary>
    internal void NotifyRootDetached()
    {
        ResetSnapshot();
        DetachOverlay();
    }

    private void ResetSnapshot()
    {
        _restoreTimer?.Dispose();
        _restoreTimer = null;
        _movingScrollers = 0;
        _captureGeneration++;
        _capturing = false;
        if (_snapshot is null)
            return;
        _snapshot = null;
        ApplyNativeHidden();
        InvalidatePaint();
        OnPropertyChanged(nameof(IsShowingSnapshot));
    }

    /// <summary>
    /// Whether the native view is hidden: a snapshot is drawn in its place, or this view is not shown (it or a drawn
    /// ancestor is invisible, e.g. the collapsed content of an expander), since the overlay sits above the surface.
    /// </summary>
    internal bool IsNativeHidden => _snapshot is not null || _notShown;

    private void ApplyNativeHidden() => SetNativeHidden(IsNativeHidden);

    // Watched only while parented: IsShown keeps tracker state for this branch, which goes with the subscription. A
    // trade-off: a tree with hosted native views always pays the tracker's walk of the watched branches on parent and
    // visibility changes (not its free path), so that native views under hidden drawn content are hidden.
    private void WatchShown(bool watch)
    {
        if (watch == _watchingShown)
            return;
        _watchingShown = watch;
        if (watch)
            IsShownChanged += OnIsShownChanged;
        else
            IsShownChanged -= OnIsShownChanged;
        OnIsShownChanged(this, EventArgs.Empty);
    }

    private void OnIsShownChanged(object? sender, EventArgs e)
    {
        var notShown = _watchingShown && !IsShown;
        if (notShown == _notShown)
            return;
        _notShown = notShown;
        if (!notShown)
            SyncOverlayBounds(); // placed before it shows again (it may have moved while hidden)
        ApplyNativeHidden();
    }

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
                var offset = scroll.VisualScrollOffset;
                x -= offset.X;
                y -= offset.Y;
            }
        }
        return new Rect(x, y, Frame.Width, Frame.Height);
    }

    /// <summary>
    /// Root-relative rectangle the overlay may draw and take touches in: the intersection of the viewports of ancestor
    /// scrollers and of ancestors that clip their children (<see cref="SkUiView.ClipToBounds"/>). Empty when scrolled out.
    /// </summary>
    internal Rect ComputeRootRelativeClip()
    {
        var clip = new Rect(double.NegativeInfinity / 2, double.NegativeInfinity / 2, double.PositiveInfinity, double.PositiveInfinity);
        var clipped = false;
        for (var ancestor = Parent as SkUiView; ancestor is not null && ancestor.Handler is null; ancestor = ancestor.Parent as SkUiView)
        {
            if (ancestor is not SkUiScrollView && !ancestor.ClipsHostedViews)
                continue;
            var rect = RootRelativeFrame(ancestor);
            // A scroller shows its content in its scrollport only (not in a reserved scroll bar gutter).
            if (ancestor is SkUiScrollView scroller)
                rect = new Rect(rect.X + scroller.Scrollport.X, rect.Y + scroller.Scrollport.Y, scroller.Scrollport.Width, scroller.Scrollport.Height);
            clip = clipped ? clip.Intersect(rect) : rect;
            clipped = true;
            if (clip.Width <= 0 || clip.Height <= 0)
                return Rect.Zero;
        }
        return clipped ? clip : new Rect(-1e6, -1e6, 2e6, 2e6);
    }

    /// <summary>Root-relative arranged rectangle of <paramref name="node"/> (translations and ancestor scroll offsets).</summary>
    private static Rect RootRelativeFrame(SkUiView node)
    {
        double x = node.Frame.X + node.TranslationX, y = node.Frame.Y + node.TranslationY;
        for (var ancestor = node.Parent as SkUiView; ancestor is not null && ancestor.Handler is null; ancestor = ancestor.Parent as SkUiView)
        {
            x += ancestor.Frame.X + ancestor.TranslationX;
            y += ancestor.Frame.Y + ancestor.TranslationY;
            if (ancestor is SkUiScrollView scroll)
            {
                var offset = scroll.VisualScrollOffset;
                x -= offset.X;
                y -= offset.Y;
            }
        }
        return new Rect(x, y, node.Frame.Width, node.Frame.Height);
    }

    /// <summary>Repositions the native overlay after an ancestor scroll offset change (no local rearrange).</summary>
    internal void NotifyAncestorScrollOffsetChanged()
    {
#if WINDOWS
        // Windows keeps the overlay a touch drag started on live (hiding it would drop the contact), even while its
        // snapshot is shown: keep it placed.
        SyncOverlayBounds();
#else
        if (_snapshot is null)
            SyncOverlayBounds(); // hidden while a snapshot is shown: restored with fresh bounds
#endif
    }

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
    partial void StartCapture(Action<SKImage?> done, ref bool started);
    partial void SetNativeHidden(bool hidden);

    /// <summary>
    /// Where the platform placed the native view, read back from it (root-relative DIPs); null when it is not attached
    /// (no platform root, or the headless target). Diagnostics and the device tests' hosted check compare it with
    /// <see cref="ComputeRootRelativeFrame"/> and <see cref="ComputeRootRelativeClip"/>.
    /// </summary>
    internal SkUiOverlayPlacement? GetNativePlacement()
    {
        SkUiOverlayPlacement? placement = null;
        ReadNativePlacement(ref placement);
        return placement;
    }

    partial void ReadNativePlacement(ref SkUiOverlayPlacement? placement);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        // The hosted native view is read by the platform itself.
        info.IsNative = true;
    }
}
