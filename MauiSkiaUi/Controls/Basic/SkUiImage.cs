using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SkiaSharp;

namespace MauiSkiaUi;

/// <summary>
/// A Skia-drawn image, similar to MAUI's Image: file (<c>MauiImage</c> or raw package asset), stream, HTTP(S) and font
/// sources, loaded through the shared image cache (<see cref="SkUiImageCache"/>), with FFImageLoading-style
/// <see cref="Transformations"/> and downsampling, EXIF orientation and animated GIF / WebP.
/// </summary>
public class SkUiImage : SkUiView
{
    private ImageSource? _source;
    private Aspect _aspect = Aspect.AspectFit;
    private SkUiImageOptions _options;
    // Image sources and transformation lists are often shared resources: listened to weakly, so they never keep the view alive.
    private readonly SkUiWeakListener<SkUiImage> _sourceListener;
    private readonly SkUiWeakListener<SkUiImage> _transformationsListener;
    private readonly SkUiImageSlot _slot;

    /// <summary>Creates an image.</summary>
    public SkUiImage()
    {
        _sourceListener = new(this, static (image, change) =>
        {
            if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
                image.Reload();
        });
        _transformationsListener = new(this, static (image, _) => image.OnTransformationItemsChanged());
        _slot = new SkUiImageSlot(this, PublishState)
        {
            LoadingStarted = args => LoadingStarted?.Invoke(this, args),
            LoadingFinished = args => LoadingFinished?.Invoke(this, args)
        };
        // The default list comes from defaultValueCreator, which raises no change: observe it here (XAML adds to it).
        OnTransformationsChanged(Transformations);
    }

    /// <summary>Bindable MAUI image source.</summary>
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnSourceChanged((ImageSource?)value));
    /// <summary>Bindable aspect mode.</summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(SkUiImage), Aspect.AspectFit,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnAspectChanged((Aspect)value));
    /// <summary>Bindable <see cref="IsAnimationPlaying"/>.</summary>
    public static readonly BindableProperty IsAnimationPlayingProperty = BindableProperty.Create(nameof(IsAnimationPlaying), typeof(bool), typeof(SkUiImage), false,
        propertyChanged: (view, _, value) => ((SkUiImage)view)._slot.IsAnimationPlaying = (bool)value);
    /// <summary>Bindable <see cref="Transformations"/>.</summary>
    public static readonly BindableProperty TransformationsProperty = BindableProperty.Create(nameof(Transformations), typeof(IList<ISkUiImageTransformation>), typeof(SkUiImage), null,
        defaultValueCreator: _ => new ObservableCollection<ISkUiImageTransformation>(),
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnTransformationsChanged((IList<ISkUiImageTransformation>?)value));
    /// <summary>Bindable <see cref="CacheType"/>.</summary>
    public static readonly BindableProperty CacheTypeProperty = BindableProperty.Create(nameof(CacheType), typeof(SkUiImageCacheType), typeof(SkUiImage), SkUiImageCacheType.All,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnOptionsChanged(((SkUiImage)view)._options with { CacheType = (SkUiImageCacheType)value }));
    /// <summary>Bindable <see cref="DownsampleWidth"/>.</summary>
    public static readonly BindableProperty DownsampleWidthProperty = BindableProperty.Create(nameof(DownsampleWidth), typeof(double), typeof(SkUiImage), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnOptionsChanged(((SkUiImage)view)._options with { DownsampleWidth = (double)value }));
    /// <summary>Bindable <see cref="DownsampleHeight"/>.</summary>
    public static readonly BindableProperty DownsampleHeightProperty = BindableProperty.Create(nameof(DownsampleHeight), typeof(double), typeof(SkUiImage), 0d,
        validateValue: SkUiValidate.NonNegative,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnOptionsChanged(((SkUiImage)view)._options with { DownsampleHeight = (double)value }));

    /// <summary>Bindable <see cref="LoadingPlaceholder"/>.</summary>
    public static readonly BindableProperty LoadingPlaceholderProperty = BindableProperty.Create(nameof(LoadingPlaceholder), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, _, _) => ((SkUiImage)view).UpdatePlaceholders());
    /// <summary>Bindable <see cref="ErrorPlaceholder"/>.</summary>
    public static readonly BindableProperty ErrorPlaceholderProperty = BindableProperty.Create(nameof(ErrorPlaceholder), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, _, _) => ((SkUiImage)view).UpdatePlaceholders());
    /// <summary>Bindable <see cref="TransformPlaceholders"/>.</summary>
    public static readonly BindableProperty TransformPlaceholdersProperty = BindableProperty.Create(nameof(TransformPlaceholders), typeof(bool), typeof(SkUiImage), true,
        propertyChanged: (view, _, _) => ((SkUiImage)view).UpdatePlaceholders());

    /// <summary>Raised when a load of <see cref="Source"/> starts (also for a memory-cache hit, which finishes at once).</summary>
    public event EventHandler<SkUiImageLoadStartedEventArgs>? LoadingStarted;
    /// <summary>
    /// Raised once for every <see cref="LoadingStarted"/>, on the UI thread after <see cref="IsLoading"/>,
    /// <see cref="LoadError"/> and <see cref="ImageSize"/> are updated: succeeded (with where the image came from),
    /// failed (with the error) or cancelled (the source changed or the view was disposed first). Placeholder loads
    /// raise no events.
    /// </summary>
    public event EventHandler<SkUiImageLoadFinishedEventArgs>? LoadingFinished;

    /// <summary>
    /// Shown while <see cref="Source"/> loads (FFImageLoading's <c>LoadingPlaceholder</c>): any source kind, through the
    /// same cache, so a <c>MauiImage</c> or a glyph shows at once. Animated placeholders always play.
    /// </summary>
    public ImageSource? LoadingPlaceholder { get => (ImageSource?)GetValue(LoadingPlaceholderProperty); set => SetValue(LoadingPlaceholderProperty, value); }
    /// <summary>Shown when <see cref="Source"/> failed to load (<see cref="LoadError"/> is set).</summary>
    public ImageSource? ErrorPlaceholder { get => (ImageSource?)GetValue(ErrorPlaceholderProperty); set => SetValue(ErrorPlaceholderProperty, value); }
    /// <summary>
    /// Whether placeholders get the same <see cref="Transformations"/> and downsampling as the image (default <c>true</c>:
    /// a circle avatar's placeholder is a circle too).
    /// </summary>
    public bool TransformPlaceholders { get => (bool)GetValue(TransformPlaceholdersProperty); set => SetValue(TransformPlaceholdersProperty, value); }

    /// <summary>
    /// Image source; loading starts when it changes. A relative <see cref="FileImageSource"/> names a <c>MauiImage</c>
    /// (the file Resizetizer made for the display density; an SVG by its <c>.png</c> name), else a raw package asset.
    /// </summary>
    public ImageSource? Source { get => (ImageSource?)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    /// <summary>Fit, fill, stretch or center (unscaled) within the arranged bounds.</summary>
    public Aspect Aspect { get => (Aspect)GetValue(AspectProperty); set => SetValue(AspectProperty, value); }
    /// <summary>Whether an animated GIF / WebP plays (MAUI's <c>IsAnimationPlaying</c>; default <c>false</c>: the first frame shows).</summary>
    public bool IsAnimationPlaying { get => (bool)GetValue(IsAnimationPlayingProperty); set => SetValue(IsAnimationPlayingProperty, value); }
    /// <summary>
    /// Applied in order to the decoded image before it is cached and drawn (FFImageLoading's <c>Transformations</c>):
    /// <see cref="SkUiCircleTransformation"/>, <see cref="SkUiRoundedTransformation"/>, … Each view has its own list;
    /// adding or removing items reloads (for the default list, or any assigned list that raises
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/>; assigning a list always reloads). A transformation that changes the pixel size (a circle crops to a square)
    /// changes the intrinsic size.
    /// </summary>
    public IList<ISkUiImageTransformation> Transformations { get => (IList<ISkUiImageTransformation>)GetValue(TransformationsProperty); set => SetValue(TransformationsProperty, value); }
    /// <summary>Which caches the load may use (default <see cref="SkUiImageCacheType.All"/>).</summary>
    public SkUiImageCacheType CacheType { get => (SkUiImageCacheType)GetValue(CacheTypeProperty); set => SetValue(CacheTypeProperty, value); }
    /// <summary>
    /// Decode at most this wide, in DIPs at the display density (0: no bound; FFImageLoading's <c>DownsampleWidth</c>
    /// with <c>DownsampleUseDipUnits</c>). Saves memory for thumbnails; the intrinsic size stays the source's.
    /// </summary>
    public double DownsampleWidth { get => (double)GetValue(DownsampleWidthProperty); set => SetValue(DownsampleWidthProperty, value); }
    /// <summary>Decode at most this high, in DIPs at the display density (0: no bound).</summary>
    public double DownsampleHeight { get => (double)GetValue(DownsampleHeightProperty); set => SetValue(DownsampleHeightProperty, value); }
    /// <summary>Current asynchronous load, including error-state publication.</summary>
    public Task LoadingTask => _slot.LoadingTask;
    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading => _slot.IsLoading;
    /// <summary>Last current-source error, or null on success.</summary>
    public Exception? LoadError => _slot.LoadError;
    /// <summary>
    /// Intrinsic size in DIPs: source pixels divided by the source's density (a MauiImage lays out at its base size,
    /// other bitmaps one pixel per DIP; a font glyph at its font size).
    /// </summary>
    /// <remarks>Large sources are decoded at reduced resolution (<see cref="SkUiImageDecoder.MaxDecodeDimension"/>, downsampling); this still reports the source size.</remarks>
    public Size ImageSize => _slot.Size;

    /// <summary>The shown cache entry (tests: views of one source share it).</summary>
    internal SkUiCachedImage? CachedImage => _slot.Entry;

    /// <summary>Sets source (same as the property setter). Call on the UI thread; streams are owned and disposed by this control.</summary>
    public SkUiImage SetSource(ImageSource? value)
    {
        Source = value;
        return this;
    }

    /// <summary>Sets the loading and error placeholders (same as the property setters).</summary>
    public SkUiImage SetPlaceholders(ImageSource? loading, ImageSource? error)
    {
        LoadingPlaceholder = loading;
        ErrorPlaceholder = error;
        return this;
    }
    /// <summary>Sets <see cref="TransformPlaceholders"/> (same as the property setter).</summary>
    public SkUiImage SetTransformPlaceholders(bool value) { TransformPlaceholders = value; return this; }

    private void UpdatePlaceholders()
    {
        if (_slot is null) return; // property defaults apply before the constructor body
        _slot.SetPlaceholders(SkUiMauiImageSources.Convert(LoadingPlaceholder), SkUiMauiImageSources.Convert(ErrorPlaceholder), TransformPlaceholders);
    }

    /// <summary>Sets aspect (same as the property setter).</summary>
    public SkUiImage SetAspect(Aspect value) { Aspect = value; return this; }
    /// <summary>Sets <see cref="IsAnimationPlaying"/> (same as the property setter).</summary>
    public SkUiImage SetIsAnimationPlaying(bool value) { IsAnimationPlaying = value; return this; }
    /// <summary>Replaces the transformations with <paramref name="transformations"/> (one reload).</summary>
    public SkUiImage SetTransformations(params ISkUiImageTransformation[] transformations)
    {
        ArgumentNullException.ThrowIfNull(transformations);
        Transformations = new ObservableCollection<ISkUiImageTransformation>(transformations);
        return this;
    }
    /// <summary>Sets <see cref="CacheType"/> (same as the property setter).</summary>
    public SkUiImage SetCacheType(SkUiImageCacheType value) { CacheType = value; return this; }
    /// <summary>Sets <see cref="DownsampleWidth"/> and <see cref="DownsampleHeight"/> in DIPs (0: no bound).</summary>
    public SkUiImage SetDownsample(double width, double height)
    {
        SkUiValidate.ThrowIfNegativeOrNotFinite(width, nameof(width));
        SkUiValidate.ThrowIfNegativeOrNotFinite(height, nameof(height));
        DownsampleWidth = width;
        DownsampleHeight = height;
        return this;
    }

    private void OnSourceChanged(ImageSource? value)
    {
        if (ReferenceEquals(_source, value)) return;
        _source = value;
        _sourceListener.Listen(value);
        Reload();
    }

    private void OnAspectChanged(Aspect value) { _aspect = value; InvalidatePaint(); }

    private void OnTransformationsChanged(IList<ISkUiImageTransformation>? value)
    {
        _transformationsListener.Listen(value as INotifyCollectionChanged);
        OnTransformationItemsChanged();
    }

    private void OnTransformationItemsChanged()
    {
        // A snapshot: later edits of the list reload, edits of a transformation's settings apply on the next load.
        var list = (IList<ISkUiImageTransformation>?)GetValue(TransformationsProperty);
        var transformations = list is { Count: > 0 } ? list.Where(static item => item is not null).ToArray() : null;
        OnOptionsChanged(_options with { Transformations = transformations });
    }

    private void OnOptionsChanged(SkUiImageOptions options)
    {
        if (_options.CacheType == options.CacheType && _options.DownsampleWidth == options.DownsampleWidth
            && _options.DownsampleHeight == options.DownsampleHeight && SameItems(_options.Transformations, options.Transformations))
            return;
        _options = options;
        Reload();
    }

    private static bool SameItems(IReadOnlyList<ISkUiImageTransformation>? a, IReadOnlyList<ISkUiImageTransformation>? b)
    {
        var count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0)) return false;
        for (var index = 0; index < count; index++)
            if (!ReferenceEquals(a![index], b![index])) return false;
        return true;
    }

    private void Reload()
    {
        if (_slot is not null) // property defaults apply before the constructor body
            _slot.Load(SkUiMauiImageSources.Convert(_source), _options);
    }

    /// <summary>Reloads the current source; errors are exposed through LoadError, cancellation is not an error.</summary>
    public Task ReloadAsync()
    {
        return _slot.Load(SkUiMauiImageSources.Convert(_source), _options);
    }

    private void PublishState()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(ImageSize));
        OnPropertyChanged(nameof(IsShowingPlaceholder));
        InvalidateMeasureOverride();
    }

    /// <summary>Whether a loading or error placeholder is drawn instead of the image.</summary>
    public bool IsShowingPlaceholder => _slot.IsShowingPlaceholder;

    /// <inheritdoc />
    /// <remarks>The image's size, else the placeholder's while one shows.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => _slot.DisplayedSize;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => PaintImage(canvas, new SKRect(0, 0, (float)Width, (float)Height));

    /// <summary>Draws the current image (frame) into <paramref name="area"/> (local DIPs) with <see cref="Aspect"/>; nothing while none is loaded.</summary>
    protected void PaintImage(SKCanvas canvas, SKRect area) => _slot.Paint(canvas, area, _aspect);
}
