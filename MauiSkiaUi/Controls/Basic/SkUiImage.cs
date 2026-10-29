using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MauiSkiaUi.Rendering;
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
    private bool _downsampleToViewSize;
    private SkUiViewSizedDecode _viewSizedDecode;
    private bool _fadeAnimationEnabled;
    private int? _fadeAnimationDuration;
    private bool _fadeAnimationForCachedImages;
    private SkUiImageFade? _fade;
    private Action<SKCanvas>? _paintFadeLayer;
    // Set on the thread pool by a load finished (or reporting) without a dispatcher, read on the view's thread (see PublishState).
    private volatile bool _stateStoreStale;
    // Image sources and transformation lists are often shared resources: listened to weakly, so they never keep the view alive.
    private SkUiWeakListener<SkUiImage>? _sourceListener;
    private SkUiWeakListener<SkUiImage> SourceListener => _sourceListener ??= new(this, static (image, change) =>
    {
        if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
            image.LoadNewSource();
    });
    private SkUiWeakListener<SkUiImage>? _transformationsListener;
    private SkUiWeakListener<SkUiImage> TransformationsListener => _transformationsListener ??= new(this, static (image, _) => image.OnTransformationItemsChanged());
    private SkUiWeakListener<SkUiImage>? _loadingPlaceholderListener;
    private SkUiWeakListener<SkUiImage>? _errorPlaceholderListener;
    private SkUiImageSlot? _slot;

    /// <summary>The loading state, created on first use: property callbacks can run before the constructor (see <see cref="SkUiView.StyledOr{T}"/>).</summary>
    private SkUiImageSlot Slot => _slot ??= new SkUiImageSlot(this, PublishState)
    {
        LoadingStarted = args => LoadingStarted?.Invoke(this, args),
        LoadingFinished = args => LoadingFinished?.Invoke(this, args),
        DownloadProgress = OnDownloadProgress,
        FadesIn = StartsFade
    };

    /// <summary>Creates an image.</summary>
    public SkUiImage()
    {
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
        propertyChanged: (view, _, value) => ((SkUiImage)view).Slot.IsAnimationPlaying = (bool)value);
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
    /// <summary>Bindable <see cref="DownsampleToViewSize"/>.</summary>
    public static readonly BindableProperty DownsampleToViewSizeProperty = BindableProperty.Create(nameof(DownsampleToViewSize), typeof(bool), typeof(SkUiImage), false,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnDownsampleToViewSizeChanged((bool)value));
    /// <summary>Bindable <see cref="FadeAnimationEnabled"/>.</summary>
    public static readonly BindableProperty FadeAnimationEnabledProperty = BindableProperty.Create(nameof(FadeAnimationEnabled), typeof(bool), typeof(SkUiImage), false,
        propertyChanged: (view, _, value) => ((SkUiImage)view)._fadeAnimationEnabled = (bool)value);
    /// <summary>Bindable <see cref="FadeAnimationDuration"/>.</summary>
    public static readonly BindableProperty FadeAnimationDurationProperty = BindableProperty.Create(nameof(FadeAnimationDuration), typeof(int?), typeof(SkUiImage), null,
        validateValue: SkUiValidate.OptionalNonNegative,
        propertyChanged: (view, _, value) => ((SkUiImage)view)._fadeAnimationDuration = (int?)value);
    /// <summary>Bindable <see cref="FadeAnimationForCachedImages"/>.</summary>
    public static readonly BindableProperty FadeAnimationForCachedImagesProperty = BindableProperty.Create(nameof(FadeAnimationForCachedImages), typeof(bool), typeof(SkUiImage), false,
        propertyChanged: (view, _, value) => ((SkUiImage)view)._fadeAnimationForCachedImages = (bool)value);
    /// <summary>Bindable <see cref="CacheKey"/>.</summary>
    public static readonly BindableProperty CacheKeyProperty = BindableProperty.Create(nameof(CacheKey), typeof(string), typeof(SkUiImage), null,
        propertyChanged: (view, _, value) => ((SkUiImage)view).OnOptionsChanged(((SkUiImage)view)._options with { CacheKey = (string?)value }));

    /// <summary>Bindable <see cref="LoadingPlaceholder"/>.</summary>
    public static readonly BindableProperty LoadingPlaceholderProperty = BindableProperty.Create(nameof(LoadingPlaceholder), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, old, value) => ((SkUiImage)view).OnPlaceholderChanged((ImageSource?)old, (ImageSource?)value));
    /// <summary>Bindable <see cref="ErrorPlaceholder"/>.</summary>
    public static readonly BindableProperty ErrorPlaceholderProperty = BindableProperty.Create(nameof(ErrorPlaceholder), typeof(ImageSource), typeof(SkUiImage), null,
        propertyChanged: (view, old, value) => ((SkUiImage)view).OnPlaceholderChanged((ImageSource?)old, (ImageSource?)value));
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
    /// <summary>
    /// Decode no larger than the view is arranged (FFImageLoading's <c>DownsampleToViewSize</c>; default <c>false</c>):
    /// fitting inside it, or covering it for <see cref="Aspect.AspectFill"/> and <see cref="Aspect.Fill"/>. A new source
    /// starts loading at the view's next arrange; a larger arrange decodes again (the smaller image shows meanwhile), a
    /// smaller one keeps the image. Bounds round up to steps of 16 DIPs, so rows of one size share cache entries. A
    /// dimension whose size comes from the image is not bounded; <see cref="DownsampleWidth"/> /
    /// <see cref="DownsampleHeight"/> win, and <see cref="Aspect.Center"/> (drawn unscaled) decodes as without it.
    /// </summary>
    public bool DownsampleToViewSize { get => (bool)GetValue(DownsampleToViewSizeProperty); set => SetValue(DownsampleToViewSizeProperty, value); }
    /// <summary>
    /// Whether a loaded image fades in over its loading placeholder (FFImageLoading's; default <c>false</c>, as MAUI's
    /// <c>Image</c>). The fade runs on the render thread with the look's <see cref="SkUiTransitionKind.ImageFadeIn"/>
    /// transition (200 ms), none while motion is reduced. Images already in the memory cache show at once (see
    /// <see cref="FadeAnimationForCachedImages"/>), and so do images loaded before the view was first drawn and images
    /// replacing a shown one (another source set in its place, a larger decode): there is nothing to fade over.
    /// </summary>
    public bool FadeAnimationEnabled { get => (bool)GetValue(FadeAnimationEnabledProperty); set => SetValue(FadeAnimationEnabledProperty, value); }
    /// <summary>The fade's length in milliseconds (<c>null</c>: the look's; 0: no fade).</summary>
    public int? FadeAnimationDuration { get => (int?)GetValue(FadeAnimationDurationProperty); set => SetValue(FadeAnimationDurationProperty, value); }
    /// <summary>Whether images from the memory cache fade in too (default <c>false</c>: they show at once).</summary>
    public bool FadeAnimationForCachedImages { get => (bool)GetValue(FadeAnimationForCachedImagesProperty); set => SetValue(FadeAnimationForCachedImagesProperty, value); }
    /// <summary>
    /// This view's cache identity for <see cref="Source"/> (FFImageLoading's <c>CacheKey</c>; <c>null</c> or empty: the
    /// app's <see cref="SkUiImageCache.CacheKeyFactory"/>, else the source's own key). Views with the same key share one
    /// entry in memory (per decode size and transformations) and, for a URI, one download on disk, whatever their
    /// sources: a URL with a token that changes, or a stream source, which is otherwise not cached. Placeholders keep
    /// their own keys. Changing it reloads; <see cref="SkUiImageCache.RemoveByKeyAsync"/> forgets it.
    /// </summary>
    public string? CacheKey { get => (string?)GetValue(CacheKeyProperty); set => SetValue(CacheKeyProperty, value); }
    /// <summary>Current asynchronous load, including error-state publication.</summary>
    public Task LoadingTask => Slot.LoadingTask;
    private static readonly BindablePropertyKey IsLoadingPropertyKey =
        BindableProperty.CreateReadOnly(nameof(IsLoading), typeof(bool), typeof(SkUiImage), false);

    /// <summary>Read-only bindable <see cref="IsLoading"/>, as MAUI's, for triggers and bindings.</summary>
    public static readonly BindableProperty IsLoadingProperty = IsLoadingPropertyKey.BindableProperty;

    /// <summary>Whether the current source is loading.</summary>
    public bool IsLoading => Slot.IsLoading;

    private static readonly BindablePropertyKey LoadingProgressPropertyKey =
        BindableProperty.CreateReadOnly(nameof(LoadingProgress), typeof(double), typeof(SkUiImage), 0d);

    /// <summary>Read-only bindable <see cref="LoadingProgress"/>, for a progress bar or a trigger.</summary>
    public static readonly BindableProperty LoadingProgressProperty = LoadingProgressPropertyKey.BindableProperty;

    /// <summary>
    /// How far the load of <see cref="Source"/> is: the fraction of a download received (0–1), -1 while downloading
    /// without a known size, 1 once the image is shown, 0 otherwise (no source, failed, or a load that has not reported).
    /// Only URIs report while loading; other sources go from 0 to 1.
    /// </summary>
    public double LoadingProgress => Slot.LoadingProgress;

    /// <summary>
    /// Raised while <see cref="Source"/> downloads, after <see cref="LoadingProgress"/> changed: on the UI thread, at most
    /// once per dispatcher turn. Cache hits raise none; a view that joined another view's download of the same image
    /// hears it too.
    /// </summary>
    public event EventHandler<SkUiImageDownloadProgressEventArgs>? DownloadProgress;

    private void OnDownloadProgress(SkUiImageDownloadProgressEventArgs args)
    {
        PublishStore();
        DownloadProgress?.Invoke(this, args);
    }

    // The bindable store of the read-only state; left alone on a thread without a dispatcher (it is not thread-safe):
    // the stored values follow on the view's next change (OnPropertyChanged), such as its layout once it is shown.
    private void PublishStore()
    {
        if (SkUiImageSlot.CompletingWithoutDispatcher)
        {
            _stateStoreStale = true;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(LoadingProgress));
        }
        else
        {
            _stateStoreStale = false;
            SetValue(IsLoadingPropertyKey, Slot.IsLoading);
            SetValue(LoadingProgressPropertyKey, Slot.LoadingProgress);
        }
    }

    /// <summary>Bindable <see cref="IsOpaque"/>.</summary>
    public static readonly BindableProperty IsOpaqueProperty = BindableProperty.Create(nameof(IsOpaque), typeof(bool), typeof(SkUiImage), false);

    /// <summary>
    /// MAUI's hint that the image has no transparent pixels, which its handlers pass to the platform image view. Accepted
    /// for MAUI markup; drawn images composite the same either way.
    /// </summary>
    public bool IsOpaque { get => (bool)GetValue(IsOpaqueProperty); set => SetValue(IsOpaqueProperty, value); }

    /// <summary>Sets <see cref="IsOpaque"/>.</summary>
    public SkUiImage SetIsOpaque(bool value) { IsOpaque = value; return this; }
    /// <summary>Last current-source error, or null on success.</summary>
    public Exception? LoadError => Slot.LoadError;
    /// <summary>
    /// Intrinsic size in DIPs: source pixels divided by the source's density (a MauiImage lays out at its base size,
    /// other bitmaps one pixel per DIP; a font glyph at its font size).
    /// </summary>
    /// <remarks>Large sources are decoded at reduced resolution (<see cref="SkUiImageDecoder.MaxDecodeDimension"/>, downsampling); this still reports the source size.</remarks>
    public Size ImageSize => Slot.Size;

    /// <summary>Whether the image is fading in (tests).</summary>
    internal bool IsFadingIn => _fade is { IsActive: true };

    /// <summary>The shown cache entry (tests: views of one source share it).</summary>
    internal SkUiCachedImage? CachedImage => Slot.Entry;

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

    // Bindings inside a placeholder (a font glyph, a URI) resolve against the image, and their changes reload it.
    private void OnPlaceholderChanged(ImageSource? oldValue, ImageSource? newValue)
    {
        AdoptImageSource(UsesSource(oldValue) ? null : oldValue, newValue);
        (_loadingPlaceholderListener ??= new(this, OnPlaceholderEdited)).Listen(LoadingPlaceholder);
        (_errorPlaceholderListener ??= new(this, OnPlaceholderEdited)).Listen(ErrorPlaceholder);
        UpdatePlaceholders();
    }

    // A source shared by several slots (the source and a placeholder) stays this image's child while any still uses it.
    private bool UsesSource(ImageSource? source) => source is not null
        && (ReferenceEquals(source, _source) || ReferenceEquals(source, LoadingPlaceholder) || ReferenceEquals(source, ErrorPlaceholder));

    private static void OnPlaceholderEdited(SkUiImage image, SkUiChange change)
    {
        if (SkUiMauiImageSources.AffectsImage(change.PropertyName))
            image.UpdatePlaceholders();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        InheritBindingContext(_source);
        InheritBindingContext(LoadingPlaceholder);
        InheritBindingContext(ErrorPlaceholder);
    }

    private void UpdatePlaceholders()
    {
        Slot.SetPlaceholders(SkUiImageSource.FromMaui(LoadingPlaceholder), SkUiImageSource.FromMaui(ErrorPlaceholder), TransformPlaceholders);
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
    /// <summary>Sets <see cref="FadeAnimationEnabled"/> and, when given, <see cref="FadeAnimationDuration"/> (milliseconds).</summary>
    public SkUiImage SetFadeAnimation(bool enabled, int? duration = null)
    {
        if (duration is < 0)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The fade duration cannot be negative.");
        FadeAnimationEnabled = enabled;
        if (duration is not null)
            FadeAnimationDuration = duration;
        return this;
    }
    /// <summary>Sets <see cref="FadeAnimationForCachedImages"/> (same as the property setter).</summary>
    public SkUiImage SetFadeAnimationForCachedImages(bool value) { FadeAnimationForCachedImages = value; return this; }
    /// <summary>Sets <see cref="DownsampleToViewSize"/> (same as the property setter).</summary>
    public SkUiImage SetDownsampleToViewSize(bool value) { DownsampleToViewSize = value; return this; }
    /// <summary>Sets <see cref="CacheKey"/> (same as the property setter).</summary>
    public SkUiImage SetCacheKey(string? value) { CacheKey = value; return this; }
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
        // Bindings inside the source (a font glyph, a URI) resolve against the image, as in MAUI.
        var previous = _source;
        _source = value;
        AdoptImageSource(UsesSource(previous) ? null : previous, value);
        SourceListener.Listen(value);
        LoadNewSource();
    }

    private void OnAspectChanged(Aspect value)
    {
        var old = _aspect;
        _aspect = value;
        InvalidatePaint();
        // Fitting and covering decode to different bounds; a centered image is not downsampled to the view.
        if (_downsampleToViewSize && (SkUiViewSizedDecode.Fills(old) != SkUiViewSizedDecode.Fills(value) || (old == Aspect.Center) != (value == Aspect.Center)))
            Reload();
    }

    private void OnDownsampleToViewSizeChanged(bool value)
    {
        _downsampleToViewSize = value;
        Reload();
    }

    private void OnTransformationsChanged(IList<ISkUiImageTransformation>? value)
    {
        TransformationsListener.Listen(value as INotifyCollectionChanged);
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
            && _options.DownsampleHeight == options.DownsampleHeight && SameItems(_options.Transformations, options.Transformations)
            && SameKey(_options.CacheKey, options.CacheKey))
            return;
        _options = options;
        Reload();
    }

    // null and empty both mean "no key of the view's own".
    private static bool SameKey(string? a, string? b) => string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : a == b;

    private static bool SameItems(IReadOnlyList<ISkUiImageTransformation>? a, IReadOnlyList<ISkUiImageTransformation>? b)
    {
        var count = a?.Count ?? 0;
        if (count != (b?.Count ?? 0)) return false;
        for (var index = 0; index < count; index++)
            if (!ReferenceEquals(a![index], b![index])) return false;
        return true;
    }

    // A new source waits for the next arrange (with DownsampleToViewSize); new settings reuse the bounds already decided.
    private void LoadNewSource() => Load(restart: true);

    private void Reload() => Load(restart: false);

    /// <summary>
    /// Reloads the current source; errors are exposed through LoadError, cancellation is not an error. With
    /// <see cref="DownsampleToViewSize"/> before the view was arranged, the load starts at its arrange.
    /// </summary>
    public Task ReloadAsync() => Load(restart: false);

    private Task Load(bool restart)
    {
        var source = SkUiImageSource.FromMaui(_source);
        if (source is null || !SkUiViewSizedDecode.AppliesTo(_downsampleToViewSize, _options, _aspect))
        {
            _viewSizedDecode.Stop();
            return Slot.Load(source, _options);
        }
        if (restart || !_viewSizedDecode.HasBounds)
        {
            // Empty until then, so the arrange that starts the load is the layout's size, not the previous image's.
            _viewSizedDecode.Wait();
            Slot.Load(null, _options);
            return Task.CompletedTask;
        }
        return Slot.Load(source, _viewSizedDecode.Apply(_options, _aspect));
    }

    /// <inheritdoc />
    protected override void ArrangeContent(Size size)
    {
        base.ArrangeContent(size);
        // A view that outgrew an image decoded at full size keeps it: larger bounds give no more pixels.
        if (_viewSizedDecode.OnArranged(size, out var grew) && !(grew && Slot.Entry is { IsReduced: false }))
            Slot.Load(SkUiImageSource.FromMaui(_source), _viewSizedDecode.Apply(_options, _aspect), keepShown: grew);
    }

    private void PublishState()
    {
        if (_fade is { IsActive: true } && !Slot.IsFading)
            EndFade(); // a new load or image replaced the one fading in
        PublishStore();
        OnPropertyChanged(nameof(LoadError));
        OnPropertyChanged(nameof(ImageSize));
        OnPropertyChanged(nameof(IsShowingPlaceholder));
        InvalidateMeasureOverride();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        // The view's thread changes it: the stored IsLoading catches up with a load finished on the thread pool.
        if (_stateStoreStale && !SkUiImageSlot.CompletingWithoutDispatcher)
        {
            _stateStoreStale = false;
            SetValue(IsLoadingPropertyKey, Slot.IsLoading);
            SetValue(LoadingProgressPropertyKey, Slot.LoadingProgress);
        }
    }

    /// <summary>Whether a loading or error placeholder is drawn instead of the image.</summary>
    public bool IsShowingPlaceholder => Slot.IsShowingPlaceholder;

    /// <inheritdoc />
    /// <remarks>The image's size, else the placeholder's while one shows.</remarks>
    protected override Size MeasureContent(double widthConstraint, double heightConstraint) => Slot.DisplayedSize;

    /// <inheritdoc />
    protected override void OnPaintContent(SKCanvas canvas) => PaintImage(canvas, new SKRect(0, 0, (float)Width, (float)Height));

    /// <summary>
    /// Draws the current image (frame) into <paramref name="area"/> (local DIPs) with <see cref="Aspect"/>; nothing while
    /// none is loaded. While the image fades in, the content shows the placeholder it fades in over (the compositor
    /// draws the image).
    /// </summary>
    protected void PaintImage(SKCanvas canvas, SKRect area)
    {
        if (Slot.IsFading && _fade is { PaintsImage: false })
            Slot.PaintPlaceholder(canvas, area, _aspect);
        else
            Slot.Paint(canvas, area, _aspect);
    }

    // Called by the slot when a load succeeds with nothing shown: whether the image fades in.
    private bool StartsFade(SkUiImageOrigin origin)
    {
        if (!_fadeAnimationEnabled || SkUiImageSlot.CompletingWithoutDispatcher
            || (origin == SkUiImageOrigin.MemoryCache && !_fadeAnimationForCachedImages))
            return false;
        if (!(_fade ??= new SkUiImageFade()).Start(this, _fadeAnimationDuration, EndFade))
            return false;
        InvalidateRender(SkUiRenderDirty.Props);
        return true;
    }

    private void EndFade()
    {
        if (_fade?.Finish() != true)
            return;
        _fade.ForgetDrawable();
        Slot.EndFade();
        InvalidatePaint();
        InvalidateRender(SkUiRenderDirty.Props);
    }

    /// <inheritdoc />
    internal override void OnGetRenderProps(ref SkUiRenderProps props)
    {
        base.OnGetRenderProps(ref props);
        if (_fade is { IsActive: true } fade)
            props.Drawable = fade.GetDrawable(Slot.Image, props.Width, props.Height, _aspect, _paintFadeLayer ??= OnPaintContent);
    }

    /// <inheritdoc />
    internal override void ReleaseDrawingResources()
    {
        base.ReleaseDrawingResources();
        _fade?.ForgetDrawable();
    }

    /// <inheritdoc />
    /// <remarks>A skeleton's bones replaced the fade's drawable (the view disposes it): the next commit makes a new one.</remarks>
    private protected override void ForgetOwnDrawable() => _fade?.ForgetDrawable();

    /// <inheritdoc />
    /// <remarks>A fade in progress ends (the image shows): its clock no longer holds the view.</remarks>
    protected override void OnAnimationRootChanged(bool subtreeDetached = false)
    {
        base.OnAnimationRootChanged(subtreeDetached);
        EndFade();
    }

    /// <inheritdoc />
    protected override void OnPopulateSemantics(SkUiSemanticsInfo info)
    {
        base.OnPopulateSemantics(info);
        info.Role = SkUiSemanticsRole.Image;
    }
}
