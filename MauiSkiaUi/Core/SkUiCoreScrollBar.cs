using MauiSkiaUi.Rendering;
using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// A scroll bar of a drawn scroller (<see cref="SkUiScrollView"/>, <see cref="SkUiCoreScrollView"/>), drawn by the look
/// (<see cref="SkUiLook.DrawScrollBar"/>, <see cref="SkUiLook.DrawScrollBarTrack"/>).
/// </summary>
/// <remarks>
/// <para>
/// Scrollers own one per axis (<c>VerticalScrollBar</c>, <c>HorizontalScrollBar</c>), pinned to their viewport edge and
/// shown by their <c>VerticalScrollBarVisibility</c> / <c>HorizontalScrollBarVisibility</c>. A bar created with the public
/// constructors follows a scroller from anywhere in the same surface (beside a card, in a header): place it with any
/// layout, set its <see cref="Visibility"/>, and set the scroller's own bar to <see cref="ScrollBarVisibility.Never"/>.
/// </para>
/// <para>
/// The bar is the track; its thumb is a child placed by a scroll link (<see cref="SkUiRenderLink"/>), so the compositor
/// moves it from the scroller's offset during render-thread flings, with no recording and no UI-thread work. Fading runs
/// on the render thread too. With <see cref="ScrollBarVisibility.Default"/> the bar shows while the content scrolls and
/// fades out after <see cref="SkUiLook.ScrollBarFadeDelay"/>.
/// </para>
/// <para>
/// Desktop: while a mouse, trackpad or pen pointer hovers the bar (<see cref="IsExpanded"/>), it grows to
/// <see cref="SkUiLook.ScrollBarExpandedThickness"/> and shows its track; dragging the thumb scrolls, and a press on the
/// track pages by one viewport towards it. Touch never hovers, so touches on the bar scroll the content as usual.
/// </para>
/// </remarks>
public sealed class SkUiCoreScrollBar : SkUiCoreNode
{
    private readonly SkUiScrollController _scroller;
    private readonly ScrollOrientation _orientation;
    private readonly Thumb _thumb;
    private readonly bool _owned;
    private ScrollBarVisibility _visibility;
    private Color? _thumbColor;
    private bool _isInteractive = true;
    private bool _isExpanded;
    private bool _isDragging;
    private bool _hovered;
    private bool _thumbAtStart;
    private SkUiRenderAnimation? _fade;
    private DragGesture? _gesture;

    /// <summary>Creates a bar that follows <paramref name="scroller"/> along <paramref name="orientation"/>, placed by the app.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="orientation"/> is not Vertical or Horizontal.</exception>
    public SkUiCoreScrollBar(SkUiScrollView scroller, ScrollOrientation orientation) : this(Host(scroller), orientation, owned: false) { }

    /// <inheritdoc cref="SkUiCoreScrollBar(SkUiScrollView, ScrollOrientation)" />
    public SkUiCoreScrollBar(SkUiCoreScrollView scroller, ScrollOrientation orientation) : this(Host(scroller), orientation, owned: false) { }

    internal SkUiCoreScrollBar(ISkUiScrollHost host, ScrollOrientation orientation, bool owned)
    {
        if (orientation is not (ScrollOrientation.Vertical or ScrollOrientation.Horizontal))
            throw new ArgumentOutOfRangeException(nameof(orientation), "A scroll bar is Vertical or Horizontal.");
        _scroller = host.Scroller;
        _orientation = orientation;
        _owned = owned;
        _thumb = new Thumb(this, host);
        _thumb.AttachTo(this);
        // Laid out in physical coordinates: the thumb's travel is the scroll offset's, which is physical too.
        SetFlowDirection(FlowDirection.LeftToRight);
        SetOpacity(0);
        _scroller.Attach(this);
    }

    private static ISkUiScrollHost Host(object scroller)
    {
        ArgumentNullException.ThrowIfNull(scroller);
        return (ISkUiScrollHost)scroller;
    }

    /// <summary>Which axis the bar follows.</summary>
    public ScrollOrientation Orientation => _orientation;

    /// <summary>
    /// When the bar shows: <see cref="ScrollBarVisibility.Default"/> while the content scrolls (or a pointer hovers it), then
    /// fading out; <see cref="ScrollBarVisibility.Always"/> while the content overflows; <see cref="ScrollBarVisibility.Never"/>.
    /// A scroller's own bars follow its <c>VerticalScrollBarVisibility</c> / <c>HorizontalScrollBarVisibility</c> instead.
    /// </summary>
    /// <exception cref="InvalidOperationException">Set on a scroller's own bar.</exception>
    public ScrollBarVisibility Visibility
    {
        get => _visibility;
        set => SetVisibility(value);
    }

    /// <summary>Thumb color; <c>null</c> (default) uses the color scheme's foreground at 40 % opacity.</summary>
    public Color? ThumbColor
    {
        get => _thumbColor;
        set => SetThumbColor(value);
    }

    /// <summary>Whether a hovering pointer expands the bar and can drag the thumb or page (default <c>true</c>).</summary>
    public bool IsInteractive
    {
        get => _isInteractive;
        set => SetIsInteractive(value);
    }

    /// <summary>A pointer hovers or drags the bar: it is thicker and shows its track.</summary>
    public bool IsExpanded => _isExpanded;

    /// <summary>The thumb is being dragged with a pointer.</summary>
    public bool IsDragging => _isDragging;

    /// <summary>Sets <see cref="Visibility"/>.</summary>
    public SkUiCoreScrollBar SetVisibility(ScrollBarVisibility value)
    {
        if (_owned)
            throw new InvalidOperationException("A scroller's own scroll bar follows its VerticalScrollBarVisibility / HorizontalScrollBarVisibility.");
        ApplyVisibility(value);
        return this;
    }

    /// <summary>Sets <see cref="ThumbColor"/>.</summary>
    public SkUiCoreScrollBar SetThumbColor(Color? value)
    {
        if (SetProperty(ref _thumbColor, value, nameof(ThumbColor)))
        {
            _thumb.InvalidatePaint();
            InvalidatePaint();
        }
        return this;
    }

    /// <summary>Sets <see cref="IsInteractive"/>.</summary>
    public SkUiCoreScrollBar SetIsInteractive(bool value)
    {
        if (SetProperty(ref _isInteractive, value, nameof(IsInteractive)) && !value)
        {
            _gesture?.Cancel();
            UpdateExpanded();
        }
        return this;
    }

    internal void ApplyVisibility(ScrollBarVisibility value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        if (!SetProperty(ref _visibility, value, nameof(Visibility)))
            return;
        OnMetricsChanged();
    }

    /// <summary>The thumb (tests and diagnostics).</summary>
    internal SkUiCoreNode ThumbNode => _thumb;

    /// <summary>The scroller can scroll along this bar's axis and the bar may show.</summary>
    private bool CanShow => _visibility != ScrollBarVisibility.Never
        && (_orientation == ScrollOrientation.Vertical ? _scroller.Vertical && _scroller.MaxY > 0 : _scroller.Horizontal && _scroller.MaxX > 0);

    private Color ResolvedThumbColor => _thumbColor ?? SkUiColorScheme.Current.DefaultForeground.WithAlpha(SkUiColorScheme.Current.DefaultForeground.Alpha * 0.4f);

    #region Scroller notifications

    /// <summary>Places a scroller's own bar: its frame (the hover strip along the viewport edge) and the thumb's edge.</summary>
    internal void ArrangeOwned(Rect frame, bool thumbAtStart)
    {
        _thumbAtStart = thumbAtStart;
        Measure(frame.Width, frame.Height);
        Arrange(frame);
        LayoutThumb();
    }

    /// <summary>The scroller's extent, viewport or orientation changed: the thumb is laid out again.</summary>
    internal void OnMetricsChanged()
    {
        LayoutThumb();
        if (!CanShow)
        {
            Hide();
            UpdateExpanded();
        }
        else if (_visibility == ScrollBarVisibility.Always)
        {
            Show();
        }
    }

    /// <summary>The offset changed: a fading bar shows, and fades out again once nothing moves.</summary>
    internal void OnScrolled(bool moving)
    {
        if (_visibility != ScrollBarVisibility.Default || !CanShow)
            return;
        Show();
        if (!moving)
            FadeIfIdle();
    }

    /// <summary>The scroller stopped moving.</summary>
    internal void OnScrollEnded()
    {
        if (_visibility == ScrollBarVisibility.Default && CanShow)
            FadeIfIdle();
    }

    private void FadeIfIdle()
    {
        if (_isExpanded || _isDragging)
            return;
        var look = SkUiLook.Current;
        FadeOut(look.ScrollBarFadeDelay, look.ScrollBarFadeDuration);
    }

    #endregion

    #region Layout and drawing

    /// <inheritdoc />
    internal override bool IsPinned => _owned;

    /// <inheritdoc />
    /// <remarks>The bar is recorded while hidden, so scrolling never records it when it shows.</remarks>
    internal override bool RecordsWhenTransparent => true;

    /// <inheritdoc />
    /// <remarks>As thick as the hover strip (<see cref="SkUiLook.ScrollBarHitThickness"/>); a layout stretches it along its axis.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint)
    {
        var look = SkUiLook.Current;
        var thickness = Math.Max(look.ScrollBarHitThickness, look.ScrollBarExpandedThickness);
        var length = look.ScrollBarMinimumThumbLength;
        return _orientation == ScrollOrientation.Vertical ? new Size(thickness, length) : new Size(length, thickness);
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size) => LayoutThumb();

    internal override void AddRenderChildren(List<ISkUiRenderable> children) => children.Add(_thumb);

    private bool Vertical => _orientation == ScrollOrientation.Vertical;

    /// <summary>The thumb's rectangle across the bar (its offset and thickness), and its track length.</summary>
    private (double Cross, double Thickness, double Track) Geometry()
    {
        var look = SkUiLook.Current;
        var across = Vertical ? Frame.Width : Frame.Height;
        var track = Vertical ? Frame.Height : Frame.Width;
        var thickness = Math.Min(across, Math.Max(0, _isExpanded ? look.ScrollBarExpandedThickness : look.ScrollBarThickness));
        // A scroller's own bar keeps its thumb at the viewport edge; a placed bar centers it.
        var margin = Math.Max(0, look.ScrollBarMargin);
        var cross = !_owned ? (across - thickness) / 2 : _thumbAtStart ? Math.Min(margin, across - thickness) : Math.Max(0, across - margin - thickness);
        return (cross, thickness, track);
    }

    private void LayoutThumb()
    {
        var (cross, thickness, track) = Geometry();
        var viewport = Vertical ? _scroller.Viewport.Height : _scroller.Viewport.Width;
        var extent = Vertical ? _scroller.Extent.Height : _scroller.Extent.Width;
        var max = Vertical ? _scroller.MaxY : _scroller.MaxX;
        var minimum = Math.Max(0, SkUiLook.Current.ScrollBarMinimumThumbLength);
        var length = extent <= 0 ? track : Math.Clamp(track * viewport / extent, Math.Min(minimum, track), track);
        var range = Math.Max(0, track - length);
        _thumb.SetTrack(max > 0 ? (float)(range / max) : 0, (float)range);
        var frame = Vertical ? new Rect(cross, 0, thickness, length) : new Rect(0, cross, length, thickness);
        _thumb.Measure(frame.Width, frame.Height);
        _thumb.Arrange(frame);
    }

    /// <inheritdoc />
    protected override void OnPaintBackground(SKCanvas canvas) { }

    /// <inheritdoc />
    /// <remarks>The track, while expanded.</remarks>
    protected override void OnPaintContent(SKCanvas canvas)
    {
        if (!_isExpanded)
            return;
        var (cross, thickness, track) = Geometry();
        var bounds = Vertical
            ? SKRect.Create((float)cross, 0, (float)thickness, (float)track)
            : SKRect.Create(0, (float)cross, (float)track, (float)thickness);
        SkUiLook.Current.DrawScrollBarTrack(canvas, new SkUiScrollBarTrackPaint(bounds, _orientation, SkUiShapePainter.ToSkColor(ResolvedThumbColor)));
    }

    #endregion

    #region Fading

    /// <summary>Makes the bar fully visible at once (cancels a fade in progress).</summary>
    private void Show()
    {
        if (_fade is null && Opacity >= 1)
            return;
        _fade?.Cancel();
        _fade = null;
        SetOpacity(1);
        // The render thread may show a partly faded bar that the UI side never saw: bring it back to 1 there too.
        var show = new Fade(TimeSpan.Zero, TimeSpan.Zero, 1);
        show.Finished = (animation, _) => RenderState.ActiveAnimations?.Remove(animation);
        SkUiRenderInvalidation.Enqueue(this, show);
    }

    private void Hide()
    {
        _fade?.Cancel();
        _fade = null;
        SetOpacity(0);
    }

    /// <summary>Keeps the bar visible for <paramref name="delay"/>, then fades it out over <paramref name="duration"/> on the render thread.</summary>
    private void FadeOut(TimeSpan delay, TimeSpan duration)
    {
        if (Opacity <= 0)
            return;
        _fade?.Cancel();
        if (delay + duration <= TimeSpan.Zero)
        {
            Hide();
            return;
        }
        var fade = new Fade(delay, duration, 0)
        {
            Finished = (animation, completed) =>
            {
                RenderState.ActiveAnimations?.Remove(animation);
                if (!ReferenceEquals(_fade, animation))
                    return;
                _fade = null;
                if (completed)
                {
                    RenderState.Acknowledge(SkUiRenderProperty.Opacity, 0);
                    SetOpacity(0);
                }
            }
        };
        _fade = fade;
        SkUiRenderInvalidation.Enqueue(this, fade);
    }

    /// <summary>
    /// Opacity held at 1 for a delay, then linear to <paramref name="target"/> (without a delay and a duration: set at once).
    /// Unlike a tween it starts from 1, not from what the bar shows, so a fade queued with a show in one frame (which
    /// cancels the show) still holds the bar fully visible.
    /// </summary>
    private sealed class Fade(TimeSpan delay, TimeSpan duration, float target) : SkUiRenderAnimation
    {
        internal override int PropertyMask => 1 << (int)SkUiRenderProperty.Opacity;

        internal override bool Advance(TimeSpan elapsed, ref SkUiRenderProps props)
        {
            if (elapsed >= delay + duration)
            {
                props.Opacity = target;
                return true;
            }
            props.Opacity = elapsed <= delay ? 1 : (float)(1 + (target - 1) * (elapsed - delay).TotalMilliseconds / duration.TotalMilliseconds);
            return false;
        }
    }

    #endregion

    #region Pointer

    /// <inheritdoc />
    internal override void OnPointerOverChanged(bool isOver)
    {
        _hovered = isOver;
        UpdateExpanded();
        if (!CanShow || !_isInteractive)
            return;
        if (isOver)
            Show();
        else if (_visibility == ScrollBarVisibility.Default && !_scroller.IsMoving)
            FadeIfIdle();
    }

    private void UpdateExpanded()
    {
        var expanded = _isInteractive && CanShow && (_hovered || _isDragging);
        if (!SetProperty(ref _isExpanded, expanded, nameof(IsExpanded)))
            return;
        InvalidatePaint();
        _thumb.InvalidatePaint();
        LayoutThumb();
    }

    private void SetDragging(bool value)
    {
        if (!SetProperty(ref _isDragging, value, nameof(IsDragging)))
            return;
        _thumb.InvalidatePaint();
        UpdateExpanded();
        if (!value && !_hovered && _visibility == ScrollBarVisibility.Default)
            FadeIfIdle();
    }

    /// <inheritdoc />
    /// <remarks>Only an expanded bar takes presses: elsewhere (and for touch, which never hovers) they reach the content.</remarks>
    internal override void CollectGestureRecognizers(List<SkUiGestureRecognizer> recognizers)
    {
        base.CollectGestureRecognizers(recognizers);
        if (_isExpanded && _isInteractive)
            recognizers.Add(_gesture ??= new DragGesture(this));
    }

    /// <summary>Drags the thumb (offset follows the pointer along the track) or pages by a viewport from a press on the track.</summary>
    private sealed class DragGesture(SkUiCoreScrollBar bar) : SkUiGestureRecognizer
    {
        private long? _pointer;
        private double _startPointer;
        private double _startOffset;

        protected internal override bool IsExclusive => true;

        protected internal override bool OnPointerPressed(SkUiPointer pointer)
        {
            if (_pointer is not null || !bar._isExpanded)
                return false;
            var local = pointer.GetPosition(bar);
            var scroller = bar._scroller;
            var along = bar.Vertical ? local.Y : local.X;
            var thumbStart = (bar.Vertical ? bar._thumb.Frame.Y : bar._thumb.Frame.X) + bar._thumb.Translation;
            var thumbEnd = thumbStart + (bar.Vertical ? bar._thumb.Frame.Height : bar._thumb.Frame.Width);
            Claim();
            scroller.StopMotion();
            scroller.ClearOverscroll();
            if (along >= thumbStart && along <= thumbEnd)
            {
                _pointer = pointer.Id;
                _startPointer = along;
                _startOffset = bar.Vertical ? scroller.Y : scroller.X;
                bar.SetDragging(true);
                scroller.Dragging = true;
            }
            else
            {
                // Page one viewport towards the press, as desktop scroll bars do.
                var page = (bar.Vertical ? scroller.Viewport.Height : scroller.Viewport.Width) * (along < thumbStart ? -1 : 1);
                _ = bar.Vertical ? scroller.ScrollToAsync(scroller.X, scroller.Y + page, animated: true) : scroller.ScrollToAsync(scroller.X + page, scroller.Y, animated: true);
            }
            return true;
        }

        protected internal override void OnPointerMoved(SkUiPointer pointer)
        {
            if (_pointer != pointer.Id)
                return;
            var local = pointer.GetPosition(bar);
            var (_, _, track) = bar.Geometry();
            var length = bar.Vertical ? bar._thumb.Frame.Height : bar._thumb.Frame.Width;
            var scroller = bar._scroller;
            var max = bar.Vertical ? scroller.MaxY : scroller.MaxX;
            var range = track - length;
            if (range <= 0)
                return;
            var offset = _startOffset + ((bar.Vertical ? local.Y : local.X) - _startPointer) * max / range;
            if (bar.Vertical)
                scroller.SetOffset(scroller.X, offset);
            else
                scroller.SetOffset(offset, scroller.Y);
        }

        protected internal override void OnPointerReleased(SkUiPointer pointer)
        {
            if (_pointer == pointer.Id)
                End();
        }

        protected internal override void OnRejected(long pointerId)
        {
            if (_pointer == pointerId)
                End();
        }

        private void End()
        {
            _pointer = null;
            bar._scroller.Dragging = false;
            bar.SetDragging(false);
        }
    }

    #endregion

    /// <summary>
    /// The thumb: drawn by the look, placed along the track by a scroll link from the scroller's children offset (the
    /// compositor evaluates it every frame; the UI side writes the same translation for hit-testing).
    /// </summary>
    private sealed class Thumb(SkUiCoreScrollBar bar, ISkUiRenderable scroller) : SkUiCoreNode
    {
        private (float Factor, float Range) _track;
        private SkUiRenderLink? _link;

        /// <summary>The current translation along the track (UI side).</summary>
        public double Translation
        {
            get
            {
                var props = SkUiRenderProps.Default;
                OnGetRenderProps(ref props);
                return bar.Vertical ? props.TranslationY : props.TranslationX;
            }
        }

        public void SetTrack(float factor, float range)
        {
            if (_track == (factor, range))
                return;
            _track = (factor, range);
            InvalidateRender(SkUiRenderDirty.Props);
        }

        protected override void OnPaintBackground(SKCanvas canvas) { }

        protected override void OnPaintContent(SKCanvas canvas) =>
            SkUiLook.Current.DrawScrollBar(canvas, new SkUiScrollBarPaint(
                new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height), bar._orientation, SkUiShapePainter.ToSkColor(bar.ResolvedThumbColor))
            {
                IsExpanded = bar._isExpanded,
                IsPressed = bar._isDragging
            });

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            var source = scroller.RenderState.Node;
            var (factor, range) = _track;
            if (_link is not { } link || !ReferenceEquals(link.Source, source)
                || (bar.Vertical ? link.FactorY : link.FactorX) != factor || (bar.Vertical ? link.MaxY : link.MaxX) != range)
            {
                link = _link = bar.Vertical
                    ? new SkUiRenderLink(source, 0, 0, 0, 0, factor, 0, 0, range)
                    : new SkUiRenderLink(source, factor, 0, 0, range, 0, 0, 0, 0);
            }
            props.Link = link;
            // The same placement on the UI side, for hit-testing and immediate painting.
            var translation = link.Evaluate(bar._scroller.ChildrenOffset.X, bar._scroller.ChildrenOffset.Y);
            props.TranslationX = translation.X;
            props.TranslationY = translation.Y;
        }
    }
}
