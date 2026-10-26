namespace MauiSkiaUi;

public partial class SkUiView
{
    /// <summary>
    /// The bindable property behind <c>SafeAreaEdges</c> (MAUI 10) on the drawn containers that have it in MAUI (layouts,
    /// content views, borders, scroll, swipe and refresh views). It is opt-in: every drawn container goes edge to edge until
    /// the app sets it (MAUI's layouts default to <c>Container</c>; drawn layouts kept 1.x's edge-to-edge default). Only a
    /// surface root applies it (<see cref="SafeAreaInset"/>): drawn views inside a surface accept it and ignore it.
    /// </summary>
    internal static readonly BindableProperty SafeAreaEdgesShared = BindableProperty.Create(
        "SafeAreaEdges", typeof(SafeAreaEdges), typeof(SkUiView), SafeAreaEdges.Default,
        propertyChanged: (bindable, _, _) => ((SkUiView)bindable).OnSafeAreaEdgesChanged(),
        defaultValueCreator: bindable => ((SkUiView)bindable).DefaultSafeAreaEdges);

    /// <summary>
    /// The value <c>SafeAreaEdges</c> reads before it is set: <c>None</c> (edge to edge), or MAUI's <c>Default</c> on the
    /// scroll view. <see cref="SafeAreaRegions.Default"/> on an edge keeps nothing clear on drawn views.
    /// </summary>
    private protected virtual SafeAreaEdges DefaultSafeAreaEdges => SafeAreaEdges.None;

    /// <summary>
    /// Test hook: the window a surface root is in (its size, the root's origin in it before this arrange, the safe area),
    /// instead of the platform's.
    /// </summary>
    [ThreadStatic] internal static Func<SkUiView, (Size Window, Point Origin, Thickness SafeArea)>? WindowPlacementOverride;

    /// <summary>What a surface root keeps for the safe area, created the first time it pads itself or follows the keyboard.</summary>
    private sealed class SafeAreaState
    {
        public Thickness Inset;
        public double KeyboardTop = double.PositiveInfinity;
        public bool RevealAfterArrange;
        public SkUiFocusManager? KeyboardFocus;
    }

    private SafeAreaState? _safeArea;
    private bool _relayingOut;

    /// <summary>
    /// The padding a surface root adds for the safe area: its overlap with the window's insets on each edge its
    /// <c>SafeAreaEdges</c> keeps clear, and the soft keyboard's cover of its bottom for <see cref="SafeAreaRegions.SoftInput"/>.
    /// Zero for drawn views inside a surface.
    /// </summary>
    private protected Thickness SafeAreaInset => _safeArea?.Inset ?? default;

    /// <summary>The safe area padding in use (tests).</summary>
    internal Thickness SafeAreaInsetInUse => SafeAreaInset;

    private void OnSafeAreaEdgesChanged()
    {
        // Applied by the next arrange of a surface root (MAUI lays it out again for it).
        if (SkiaParent is null)
            InvalidateMeasure();
    }

    /// <summary>
    /// The soft keyboard begins at <paramref name="surfaceY"/> (surface DIPs; positive infinity: none), reported by the focus
    /// manager of the surface this view is the root of: a root that keeps its bottom clear of it lays out again.
    /// </summary>
    internal void OnKeyboardTopChanged(double surfaceY, SkUiFocusManager focus)
    {
        if (!IsSoftInput(SafeAreaEdgesValue.Bottom))
            return;
        var state = _safeArea ??= new SafeAreaState();
        state.KeyboardTop = surfaceY;
        state.KeyboardFocus = focus;
        InvalidateMeasure();
    }

    /// <summary>
    /// After a root made room for the keyboard: the focus manager keeps the focused field in view again, in the smaller
    /// viewports (its drawn scrollers no longer need room of their own for the keyboard).
    /// </summary>
    private void RevealFocusedFieldIfPadded()
    {
        if (_safeArea is not { RevealAfterArrange: true } state)
            return;
        state.RevealAfterArrange = false;
        state.KeyboardFocus?.RevealFocusedField();
    }

    private SafeAreaEdges SafeAreaEdgesValue => (SafeAreaEdges)GetValue(SafeAreaEdgesShared);

    /// <summary>
    /// A surface root is arranged to <paramref name="frame"/> by its native parent: takes the safe area insets of the window
    /// again, and returns whether the padding changed (the root then measures again).
    /// </summary>
    private bool UpdateSafeAreaInset(Rect frame)
    {
        if (SkiaParent is not null || _relayingOut)
            return false;
        var edges = SafeAreaEdgesValue;
        var (left, top, right, bottom) = (edges.Left, edges.Top, edges.Right, edges.Bottom);
        var inset = default(Thickness);
        // Nothing to keep clear (content views by default) asks the platform for nothing.
        if (IsContainer(left) || IsContainer(top) || IsContainer(right) || IsContainer(bottom) || IsSoftInput(bottom))
        {
            var (window, origin, safeArea) = WindowPlacementOverride?.Invoke(this)
                ?? (SkUiWindowMetrics.WindowSize(this), SkUiWindowMetrics.SurfaceOrigin(this), SkUiWindowMetrics.SafeAreaInsets(this));
            // The native view still has the previous frame: its parent's origin plus the new frame's position.
            var placed = new Rect(origin.X - Frame.X + frame.X, origin.Y - Frame.Y + frame.Y, frame.Width, frame.Height);
            inset = SafeAreaPadding((left, top, right, bottom), placed, window, safeArea, _safeArea?.KeyboardTop ?? double.PositiveInfinity);
        }
        // Exact: an unchanged padding keeps the layout.
        if (inset == SafeAreaInset)
            return false;
        var state = _safeArea ??= new SafeAreaState();
        state.RevealAfterArrange |= state.KeyboardFocus is not null && inset.Bottom != state.Inset.Bottom;
        state.Inset = inset;
        return true;
    }

    // MAUI's own tests (SafeAreaEdges.IsContainer / IsSoftInput) are internal.
    private static bool IsContainer(SafeAreaRegions region) => Includes(region, SafeAreaRegions.Container);

    private static bool IsSoftInput(SafeAreaRegions region) => Includes(region, SafeAreaRegions.SoftInput);

    private static bool Includes(SafeAreaRegions region, SafeAreaRegions flag) =>
        region == SafeAreaRegions.All || (region != SafeAreaRegions.Default && (region & flag) == flag);

    /// <summary>
    /// The padding of a view at <paramref name="placed"/> in a window of <paramref name="window"/> with
    /// <paramref name="safeArea"/> insets: on each edge kept clear of the bars and notch, how far the insets reach into the
    /// view; at the bottom, with <see cref="SafeAreaRegions.SoftInput"/>, also the keyboard's cover (its top at
    /// <paramref name="keyboardTop"/> in the view's coordinates). An unknown window size (zero) keeps only the start edges.
    /// </summary>
    internal static Thickness SafeAreaPadding((SafeAreaRegions Left, SafeAreaRegions Top, SafeAreaRegions Right, SafeAreaRegions Bottom) regions,
        Rect placed, Size window, Thickness safeArea, double keyboardTop)
    {
        var known = window.Width > 0 && window.Height > 0;
        var left = IsContainer(regions.Left) ? Math.Max(0, safeArea.Left - placed.X) : 0;
        var top = IsContainer(regions.Top) ? Math.Max(0, safeArea.Top - placed.Y) : 0;
        var right = known && IsContainer(regions.Right) ? Math.Max(0, placed.Right - (window.Width - safeArea.Right)) : 0;
        var bottom = known && IsContainer(regions.Bottom) ? Math.Max(0, placed.Bottom - (window.Height - safeArea.Bottom)) : 0;
        if (IsSoftInput(regions.Bottom) && double.IsFinite(keyboardTop))
            bottom = Math.Max(bottom, placed.Height - keyboardTop);
        return new Thickness(
            Math.Min(left, placed.Width), Math.Min(top, placed.Height), Math.Min(right, placed.Width), Math.Min(Math.Max(0, bottom), placed.Height));
    }
}
