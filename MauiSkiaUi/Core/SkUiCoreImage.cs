using SkiaSharp;

namespace MauiSkiaUi.Core;

/// <summary>
/// Core image node: an <see cref="SkUiImageSource"/> (file / <c>MauiImage</c>, URI, stream, font glyph) loaded through
/// the image cache shared with the SkUi* controls (<see cref="SkUiImageCache"/>), or a decoded <see cref="SKImage"/>.
/// Supports transformations, downsampling, EXIF orientation and animated GIF / WebP, as <c>SkUiImage</c>.
/// Does not use MAUI <c>ImageSource</c> — keep decoding outside Controls/XAML.
/// </summary>
public class SkUiCoreImage : SkUiCoreNode
{
    private readonly SkUiImageSlot _slot;
    private SkUiImageSource? _source;
    private SkUiImageOptions _options;
    private Aspect _aspect = Aspect.AspectFit;

    /// <summary>Creates an image node.</summary>
    public SkUiCoreImage() => _slot = new SkUiImageSlot(this, PublishState)
    {
        LoadingStarted = args => LoadingStarted?.Invoke(this, args),
        LoadingFinished = args => LoadingFinished?.Invoke(this, args)
    };

    /// <summary>Raised when a load of <see cref="Source"/> starts (also for a memory-cache hit, which finishes at once).</summary>
    public event EventHandler<SkUiImageLoadStartedEventArgs>? LoadingStarted;

    /// <summary>
    /// Raised once for every <see cref="LoadingStarted"/>, after the state is updated: succeeded (with where the image
    /// came from), failed or cancelled (see <c>SkUiImage.LoadingFinished</c>).
    /// </summary>
    public event EventHandler<SkUiImageLoadFinishedEventArgs>? LoadingFinished;

    private SkUiImageSource? _loadingPlaceholder;
    private SkUiImageSource? _errorPlaceholder;
    private bool _transformPlaceholders = true;

    /// <summary>Shown while <see cref="Source"/> loads (see <c>SkUiImage.LoadingPlaceholder</c>).</summary>
    public SkUiImageSource? LoadingPlaceholder { get => _loadingPlaceholder; set => SetLoadingPlaceholder(value); }

    /// <summary>Shown when <see cref="Source"/> failed to load.</summary>
    public SkUiImageSource? ErrorPlaceholder { get => _errorPlaceholder; set => SetErrorPlaceholder(value); }

    /// <summary>Whether placeholders get the image's transformations and downsampling (default <c>true</c>).</summary>
    public bool TransformPlaceholders { get => _transformPlaceholders; set => SetTransformPlaceholders(value); }

    /// <summary>Whether a loading or error placeholder is drawn instead of the image.</summary>
    public bool IsShowingPlaceholder => _slot.IsShowingPlaceholder;

    /// <summary>Sets the loading placeholder.</summary>
    public SkUiCoreImage SetLoadingPlaceholder(SkUiImageSource? value) => SetPlaceholder(ref _loadingPlaceholder, value, nameof(LoadingPlaceholder));

    /// <summary>Sets the error placeholder.</summary>
    public SkUiCoreImage SetErrorPlaceholder(SkUiImageSource? value) => SetPlaceholder(ref _errorPlaceholder, value, nameof(ErrorPlaceholder));

    /// <summary>Sets whether placeholders get the image's transformations and downsampling.</summary>
    public SkUiCoreImage SetTransformPlaceholders(bool value)
    {
        if (SetProperty(ref _transformPlaceholders, value, nameof(TransformPlaceholders)))
            _slot.SetPlaceholders(_loadingPlaceholder, _errorPlaceholder, value);
        return this;
    }

    private SkUiCoreImage SetPlaceholder(ref SkUiImageSource? field, SkUiImageSource? value, string name)
    {
        if (SetProperty(ref field, value, name))
            _slot.SetPlaceholders(_loadingPlaceholder, _errorPlaceholder, _transformPlaceholders);
        return this;
    }

    /// <summary>Fit, fill, stretch or center (unscaled) within the arranged bounds.</summary>
    public Aspect Aspect
    {
        get => _aspect;
        set => SetAspect(value);
    }

    /// <summary>The source being shown, or <c>null</c> (also after <see cref="SetImage"/>).</summary>
    public SkUiImageSource? Source => _source;

    /// <summary>Whether an animated GIF / WebP plays (default <c>false</c>: the first frame shows).</summary>
    public bool IsAnimationPlaying
    {
        get => _slot.IsAnimationPlaying;
        set => SetIsAnimationPlaying(value);
    }

    /// <summary>Transformations applied in order to the decoded image (see <c>SkUiImage.Transformations</c>).</summary>
    public IReadOnlyList<ISkUiImageTransformation> Transformations => _options.Transformations ?? [];

    /// <summary>Which caches the load may use (default <see cref="SkUiImageCacheType.All"/>).</summary>
    public SkUiImageCacheType CacheType
    {
        get => _options.CacheType;
        set => SetCacheType(value);
    }

    /// <summary>Decode at most this wide, in DIPs at the display density (0: no bound).</summary>
    public double DownsampleWidth => _options.DownsampleWidth;

    /// <summary>Decode at most this high, in DIPs at the display density (0: no bound).</summary>
    public double DownsampleHeight => _options.DownsampleHeight;

    /// <summary>Current asynchronous load task (completed when idle).</summary>
    public Task LoadingTask => _slot.LoadingTask;

    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading => _slot.IsLoading;

    /// <summary>Last load error for the current source, or <c>null</c> on success.</summary>
    public Exception? LoadError => _slot.LoadError;

    /// <summary>
    /// Intrinsic size in DIPs: source pixels divided by the source's density (a MauiImage lays out at its base size,
    /// other bitmaps one pixel per DIP; a font glyph at its font size).
    /// </summary>
    /// <remarks>Large sources are decoded at reduced resolution (<see cref="SkUiImageDecoder.MaxDecodeDimension"/>, downsampling); this still reports the source size.</remarks>
    public Size ImageSize => _slot.Size;

    /// <summary>The shown cache entry (tests: views of one source share it).</summary>
    internal SkUiCachedImage? CachedImage => _slot.Entry;

    /// <summary>Sets aspect mode.</summary>
    public SkUiCoreImage SetAspect(Aspect value)
    {
        if (!SetProperty(ref _aspect, value, nameof(Aspect))) return this;
        InvalidatePaint();
        return this;
    }

    /// <summary>Plays or pauses an animated image.</summary>
    public SkUiCoreImage SetIsAnimationPlaying(bool value)
    {
        if (_slot.IsAnimationPlaying == value) return this;
        _slot.IsAnimationPlaying = value;
        OnPropertyChanged(nameof(IsAnimationPlaying));
        return this;
    }

    /// <summary>
    /// Assigns a decoded image. When <paramref name="ownsImage"/> is <c>true</c>, this node disposes it when it is replaced or cleared, or when the node is collected.
    /// </summary>
    public SkUiCoreImage SetImage(SKImage? image, bool ownsImage = true)
    {
        _source = null;
        _slot.SetImage(image, ownsImage);
        return this;
    }

    /// <summary>Loads <paramref name="source"/> (<c>null</c> clears); an equal source already shown does not reload.</summary>
    public SkUiCoreImage SetSource(SkUiImageSource? source)
    {
        if (source is not null && Equals(_source, source) && (_slot.Entry is not null || _slot.IsLoading))
            return this;
        _source = source;
        _slot.Load(source, _options);
        return this;
    }

    /// <summary>
    /// Loads an absolute file, or a package name: the <c>MauiImage</c> for the display density, else a raw package
    /// asset (see <see cref="SkUiImageSource.FromFile"/>).
    /// </summary>
    public SkUiCoreImage SetSourceFile(string path) => SetSource(SkUiImageSource.FromFile(path));

    /// <summary>
    /// Loads an image from a stream factory. The returned stream is disposed after decode. Without
    /// <paramref name="cacheKey"/> each load decodes (not cached); with one, nodes with the same key share the image.
    /// </summary>
    public SkUiCoreImage SetSourceStream(Func<CancellationToken, Task<Stream>> open, string? cacheKey = null) =>
        SetSource(SkUiImageSource.FromStream(open, cacheKey));

    /// <summary>
    /// Loads an image from an HTTP(S) URI (plain http needs the platform's cleartext permission), kept in the disk
    /// cache for <paramref name="cacheValidity"/> (default one day) when <paramref name="cachingEnabled"/>.
    /// </summary>
    public SkUiCoreImage SetSourceUri(Uri uri, bool cachingEnabled = true, TimeSpan? cacheValidity = null) =>
        SetSource(SkUiImageSource.FromUri(uri, cachingEnabled, cacheValidity));

    /// <summary>Shows a font glyph (MAUI's <c>FontImageSource</c>; see <see cref="SkUiImageSource.FromFont"/>).</summary>
    public SkUiCoreImage SetSourceFont(string glyph, string? fontFamily = null, double size = SkUiFontImageSource.DefaultSize, Color? color = null,
        FontAttributes fontAttributes = FontAttributes.None) =>
        SetSource(SkUiImageSource.FromFont(glyph, fontFamily, size, color, fontAttributes));

    /// <summary>Replaces the transformations (reloads the source).</summary>
    public SkUiCoreImage SetTransformations(params ISkUiImageTransformation[] transformations)
    {
        ArgumentNullException.ThrowIfNull(transformations);
        var list = transformations.Length == 0 ? null : (ISkUiImageTransformation[])transformations.Clone();
        if (list is not null && Array.IndexOf(list, null) >= 0)
            throw new ArgumentException("Transformations cannot be null.", nameof(transformations));
        return SetOptions(_options with { Transformations = list }, nameof(Transformations));
    }

    /// <summary>Sets the caches the load may use (reloads the source).</summary>
    public SkUiCoreImage SetCacheType(SkUiImageCacheType value) => SetOptions(_options with { CacheType = value }, nameof(CacheType));

    /// <summary>Sets the decode bounds in DIPs (0: no bound; reloads the source).</summary>
    public SkUiCoreImage SetDownsample(double width, double height)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(width, nameof(width));
        SkUiValidate.ThrowIfNegativeOrNotFinite(height, nameof(height));
        return SetOptions(_options with { DownsampleWidth = width, DownsampleHeight = height }, nameof(DownsampleWidth));
    }

    private SkUiCoreImage SetOptions(SkUiImageOptions options, string property)
    {
        if (_options == options) return this;
        _options = options;
        OnPropertyChanged(property);
        if (_source is not null)
            _slot.Load(_source, _options);
        return this;
    }

    /// <summary>Loads the current source again (for example after <see cref="SkUiImageCache.RemoveAsync(SkUiImageSource)"/>).</summary>
    public Task ReloadAsync()
    {
        return _source is null ? Task.CompletedTask : _slot.Load(_source, _options);
    }

    /// <summary>Clears the current image and source.</summary>
    public SkUiCoreImage Clear()
    {
        _source = null;
        _slot.Load(null, _options);
        return this;
    }

    private void PublishState()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(ImageSize));
        OnPropertyChanged(nameof(IsShowingPlaceholder));
        InvalidateMeasure();
    }

    /// <inheritdoc />
    /// <remarks>The image's size, else the placeholder's while one shows.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _slot.DisplayedSize;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => PaintImage(canvas, new SKRect(0, 0, (float)Frame.Width, (float)Frame.Height));

    /// <summary>Draws the current image (frame) into <paramref name="area"/> (local DIPs) with <see cref="Aspect"/>; nothing while none is set.</summary>
    protected void PaintImage(SKCanvas canvas, SKRect area) => _slot.Paint(canvas, area, _aspect);

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Image;
    }
}
