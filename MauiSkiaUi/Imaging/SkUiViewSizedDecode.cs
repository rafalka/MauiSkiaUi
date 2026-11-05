using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// Downsampling to the view's size (<c>DownsampleToViewSize</c>), shared by the image views of both layers: a load of a
/// new source waits for the view's next arrange and decodes no larger than the arranged size; a larger arrange decodes
/// again, a smaller one keeps the image. A vector does not need the option: it follows the size the view shows it at in
/// exact pixels, larger or smaller (<see cref="FollowVector"/>).
/// </summary>
/// <remarks>
/// While a load waits, the view shows nothing and measures as empty, so the arrange that starts it is the layout's size,
/// not the previous image's. A dimension arranged at 0 (the view's size comes from its image) is not bounded.
/// </remarks>
internal struct SkUiViewSizedDecode
{
    /// <summary>Bounds round up to whole steps (DIPs), so rows of one size and small layout changes share cache entries.</summary>
    internal const double Step = 16;

    private Size _bounds; // DIPs per dimension; 0: not bounded
    private bool _decided;
    private SKSizeI _vectorPixels; // what the last redraw of a vector asked for; zero: what the entry has
    private bool _sizedAsVector; // the current load was bounded because its source says it is a vector

    /// <summary>Whether a source waits for the view's next arrange to start loading.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>Whether an arrange decided the bounds of the current source (a reload uses them).</summary>
    public readonly bool HasBounds => _decided;

    /// <summary>A new source (or new settings): the load waits for the next arrange.</summary>
    public void Wait()
    {
        IsWaiting = true;
        _decided = false;
    }

    /// <summary>Not in use (the option is off, no source, or explicit decode bounds win).</summary>
    public void Stop()
    {
        IsWaiting = false;
        _decided = false;
        _sizedAsVector = false;
    }

    /// <summary>
    /// The view was arranged at <paramref name="size"/>: whether to load now, at the first arrange or when the view
    /// outgrew the decoded bounds (<paramref name="grew"/>: keep the shown image until the larger one has loaded).
    /// </summary>
    public bool OnArranged(Size size, out bool grew)
    {
        grew = false;
        if (IsWaiting)
        {
            IsWaiting = false;
            _decided = true;
            _bounds = new Size(Bucket(size.Width), Bucket(size.Height));
            return true;
        }
        if (!_decided)
            return false;
        var bounds = new Size(Grow(_bounds.Width, size.Width), Grow(_bounds.Height, size.Height));
        // Whole steps or 0: exact comparison is what decides a reload.
        if (bounds == _bounds)
            return false;
        _bounds = bounds;
        grew = true;
        return true;
    }

    /// <summary>A new load of the source or new settings: the next vector shown is compared with its own pixels.</summary>
    public void ForgetVector()
    {
        _vectorPixels = default;
        _sizedAsVector = false;
    }

    /// <summary>The load going out is bounded by the view because its source says it is a vector (<see cref="ForVectorSource"/>).</summary>
    public void MarkSizedAsVector() => _sizedAsVector = true;

    /// <summary>Whether the last load was bounded that way and <see cref="TakeSizedBitmap"/> has not been asked.</summary>
    public readonly bool IsSizedAsVector => _sizedAsVector;

    /// <summary>
    /// Whether a source that says it is a vector turned out to be a bitmap (a <c>MauiImage</c> of that name won), bounded for
    /// nothing: the view loads it again as any bitmap, once.
    /// </summary>
    public bool TakeSizedBitmap()
    {
        var sized = _sizedAsVector;
        _sizedAsVector = false;
        return sized;
    }

    /// <summary>
    /// Whether <paramref name="source"/> is known to be a vector before it loads and the view has a size: then it is drawn for
    /// that size at once, so a new color or a recycled row does not pass through the file's size.
    /// </summary>
    public static bool ForVectorSource(SkUiImageSource? source, Size size, in SkUiImageOptions options, Aspect aspect, out SkUiImageOptions sized)
    {
        sized = options;
        if (source is not SkUiVectorSource || !AppliesTo(enabled: true, options, aspect) || !(size.Width > 0 && size.Height > 0))
            return false;
        sized = options with { DownsampleWidth = size.Width, DownsampleHeight = size.Height, DownsampleToFill = Fills(aspect) };
        return true;
    }

    /// <summary>
    /// Whether a vector of <paramref name="intrinsic"/> size and <paramref name="shown"/> pixels, shown at
    /// <paramref name="size"/>, should be drawn again, with the options for it: when its pixels differ by more than one from
    /// what that size needs. Remembering what it asked for is what stops a redraw that failed from repeating at the same size.
    /// Explicit decode bounds win, and a centered image is drawn unscaled.
    /// </summary>
    public bool FollowVector(Size size, Size intrinsic, SKSizeI shown, in SkUiImageOptions options, Aspect aspect, out SkUiImageOptions redraw)
    {
        redraw = options;
        if (!AppliesTo(enabled: true, options, aspect) || !(size.Width > 0 && size.Height > 0))
            return false;
        var scale = SkUiImageLoader.DisplayScale;
        var fill = Fills(aspect);
        var bounds = new SKSizeI(SkUiImageLoader.Pixels(size.Width, scale), SkUiImageLoader.Pixels(size.Height, scale));
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;
        var wanted = SkUiPackageImages.RenderSize(intrinsic.Width * scale, intrinsic.Height * scale, bounds, fill);
        var current = _vectorPixels != default ? _vectorPixels : shown;
        if (Math.Abs(wanted.Width - current.Width) <= 1 && Math.Abs(wanted.Height - current.Height) <= 1)
            return false;
        _vectorPixels = wanted;
        redraw = options with { DownsampleWidth = size.Width, DownsampleHeight = size.Height, DownsampleToFill = fill };
        return true;
    }

    /// <summary>
    /// <paramref name="options"/> bounded by the arranged size: fitting inside it, or covering it for an
    /// <paramref name="aspect"/> that fills.
    /// </summary>
    public readonly SkUiImageOptions Apply(in SkUiImageOptions options, Aspect aspect) => options with
    {
        DownsampleWidth = _bounds.Width,
        DownsampleHeight = _bounds.Height,
        DownsampleToFill = Fills(aspect)
    };

    /// <summary>Whether downsampling to the view applies: explicit decode bounds win, and a centered image is drawn unscaled.</summary>
    public static bool AppliesTo(bool enabled, in SkUiImageOptions options, Aspect aspect) =>
        enabled && options.DownsampleWidth <= 0 && options.DownsampleHeight <= 0 && aspect != Aspect.Center;

    /// <summary>Whether <paramref name="aspect"/> covers the view (decoded to cover, not to fit).</summary>
    public static bool Fills(Aspect aspect) => aspect is Aspect.AspectFill or Aspect.Fill;

    private static double Grow(double bound, double size) => bound <= 0 ? 0 : Math.Max(bound, Bucket(size));

    private static double Bucket(double size) => size > 0 && double.IsFinite(size) ? Math.Ceiling(size / Step) * Step : 0;
}
