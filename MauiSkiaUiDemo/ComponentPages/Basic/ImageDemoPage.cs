using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side property playground for <see cref="SkUiImage"/>: every source kind (raw asset, MauiImage PNG and SVG,
/// font glyph, web, animated GIF, stream), animation, SkiaUi's transformations and downsampling, and the shared caches. A
/// Core image shows the same MAUI source, passed straight to <see cref="SkUiCoreImage.SetSource"/>.
/// </summary>
public sealed class ImageDemoPage : ComponentDemoPage
{
    private readonly SkUiImage _skia;
    private readonly Image _native;
    private readonly SkUiCoreImage _core = new SkUiCoreImage().SetAspect(Aspect.AspectFit);
    private string _sample = SampleEarth;
    private string _transformation = TransformNone;
    private string _lastLoad = "";

    /// <summary>Raw package asset (Resources/Raw) by file name.</summary>
    public const string SampleEarth = "Earth";
    /// <summary>MauiImage PNG with a base size (lays out at 300 × 185 DIPs).</summary>
    public const string SampleMauiImage = "MauiImage";
    /// <summary>MauiImage from an SVG, referenced by its generated PNG name.</summary>
    public const string SampleSvg = "MauiImage SVG";
    /// <summary>FontImageSource glyph.</summary>
    public const string SampleGlyph = "Glyph";
    /// <summary>UriImageSource download (disk-cached).</summary>
    public const string SampleWeb = "Web";
    /// <summary>Animated GIF from a stream.</summary>
    public const string SampleAnimated = "Animated";
    /// <summary>Android vector drawable (XML) by resource name; listed on Android only.</summary>
    public const string SampleAndroidVector = "Android vector";
    /// <summary>Clears the image source.</summary>
    public const string SampleEmpty = "Empty";
    /// <summary>Feeds undecodable bytes to exercise load failure.</summary>
    public const string SampleInvalid = "Invalid";

    /// <summary>No transformation.</summary>
    public const string TransformNone = "None";

    private static readonly Dictionary<string, Func<ISkUiImageTransformation>> TransformationChoices = new()
    {
        [TransformNone] = null!,
        ["Circle"] = () => new SkUiCircleTransformation(4, DemoColors.Accent),
        ["Rounded"] = () => new SkUiRoundedTransformation(24),
        ["Grayscale"] = () => new SkUiGrayscaleTransformation(),
        ["Sepia"] = () => new SkUiSepiaTransformation(),
        ["Blur"] = () => new SkUiBlurTransformation(6),
        ["Tint"] = () => new SkUiTintTransformation(DemoColors.Accent),
        ["Rotate 90°"] = () => new SkUiRotateTransformation(90),
    };

    private static readonly byte[] Spinner = DemoGif.Spinner(96);

    // Explicit preview sizes: auto-sizing measures the control when the page loads, and a cached source is already
    // there then (Earth: 2048 × 2048 DIPs).
    public ImageDemoPage() : base(nameof(SkUiImage), new SkUiImage(), new Image(), widthRange: (40, 480, 280), heightRange: (40, 480, 150))
    {
        _skia = (SkUiImage)SkiaControl;
        _native = (Image)NativeControl!;
        Choice(nameof(SkUiImage.Aspect), new[] { Aspect.AspectFit, Aspect.AspectFill, Aspect.Fill, Aspect.Center }, Aspect.AspectFit,
            value => { _skia.Aspect = value; _native.Aspect = value; }, () => _skia.Aspect, () => _native.Aspect);
        string[] samples = DeviceInfo.Platform == DevicePlatform.Android
            ? [SampleEarth, SampleMauiImage, SampleSvg, SampleAndroidVector, SampleGlyph, SampleWeb, SampleAnimated, SampleEmpty, SampleInvalid]
            : [SampleEarth, SampleMauiImage, SampleSvg, SampleGlyph, SampleWeb, SampleAnimated, SampleEmpty, SampleInvalid];
        Choice(nameof(SkUiImage.Source), samples,
            SampleEarth, SetSample, () => _sample);
        Toggle(nameof(SkUiImage.IsAnimationPlaying), true,
            value => { _skia.IsAnimationPlaying = value; _native.IsAnimationPlaying = value; }, () => _skia.IsAnimationPlaying, () => _native.IsAnimationPlaying);
        Choice(nameof(SkUiImage.Transformations) + " (SkiaUi)", [.. TransformationChoices.Keys], TransformNone, SetTransformation, () => _transformation);
        Number(nameof(SkUiImage.DownsampleWidth) + " (SkiaUi)", 0, 300, 0, value => _skia.DownsampleWidth = value, () => _skia.DownsampleWidth, whole: true);
        // Resize the preview area: a larger size decodes again, a smaller one keeps the image ("loaded from …" below).
        Toggle(nameof(SkUiImage.FadeAnimationEnabled) + " (SkiaUi)", false, value => _skia.SetFadeAnimation(value).SetFadeAnimationForCachedImages(value),
            () => _skia.FadeAnimationEnabled); // cached images too: pick Empty, then an image (one replacing a shown image does not fade)
        Toggle(nameof(SkUiImage.DownsampleToViewSize) + " (SkiaUi)", false, value => _skia.DownsampleToViewSize = value, () => _skia.DownsampleToViewSize);
        Toggle("Placeholders (SkiaUi)", true, value => _skia.SetPlaceholders(
                value ? ImageSource.FromStream(() => new MemoryStream(Spinner)) : null,
                value ? new FontImageSource { Glyph = "\u26A0", FontFamily = DemoFonts.OpenSansRegular, Size = 64, Color = DemoColors.Fail } : null),
            () => _skia.LoadingPlaceholder is not null);
        _skia.LoadingFinished += (_, args) =>
        {
            _lastLoad = args.Status switch
            {
                SkUiImageLoadStatus.Succeeded => $"loaded from {args.Origin} in {args.Elapsed.TotalMilliseconds:F0} ms",
                SkUiImageLoadStatus.Failed => $"failed after {args.Elapsed.TotalMilliseconds:F0} ms",
                _ => "cancelled"
            };
            UpdateImageStatus();
        };
        _skia.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(SkUiImage.IsLoading) or nameof(SkUiImage.LoadError)) UpdateImageStatus(); };
        _skia.DownloadProgress += (_, _) => UpdateImageStatus(); // the web source shows its percentage while it downloads
        _native.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Image.IsLoading)) UpdateImageStatus(); };
        AddEditor("Core image, same MAUI source (SkUiCoreImage.SetSource)",
            new SkUiCoreHost { HeightRequest = 80, HorizontalOptions = LayoutOptions.Start, WidthRequest = 160 }.SetContent(_core));
        ActionButton("Reload image", () => SetSample(_sample));
        ActionButton("Clear memory cache", () => { SkUiImageCache.ClearMemoryCache(); UpdateImageStatus(); });
        ActionButton("Clear all image caches", async () =>
        {
            await SkUiImageCache.ClearAsync();
            UpdateImageStatus();
        });
        UpdateImageStatus();
    }

    private void SetTransformation(string value)
    {
        _transformation = value;
        _skia.Transformations.Clear();
        if (TransformationChoices[value] is { } create)
            _skia.Transformations.Add(create());
    }

    private void SetSample(string value)
    {
        _sample = value;
        ImageSource? Source() => value switch
        {
            SampleEmpty => null,
            SampleEarth => ImageSource.FromFile(DemoAssets.EarthImage),
            SampleMauiImage => ImageSource.FromFile(DemoAssets.DotnetBotImage),
            SampleSvg => ImageSource.FromFile(DemoAssets.BadgeImage),
            SampleAndroidVector => ImageSource.FromFile(DemoAssets.AndroidVectorImage),
            SampleGlyph => new FontImageSource { Glyph = "♥", FontFamily = DemoFonts.OpenSansRegular, Size = 96, Color = DemoColors.Accent },
            SampleWeb => ImageSource.FromUri(new Uri(DemoAssets.WebImage)),
            SampleAnimated => ImageSource.FromStream(() => new MemoryStream(Spinner)),
            _ => ImageSource.FromStream(() => new MemoryStream([1, 2, 3]))
        };
        _skia.Source = Source();
        _native.Source = Source();
        _core.SetSource(Source()); // a MAUI ImageSource converts implicitly (SkUiImageSource.FromMaui)
    }

    // MAUI's Image on Android reads drawables and files, not Resources/Raw assets (SkiaUi's file sources find those too).
    private string NativeStatus() => _sample == SampleEarth && DeviceInfo.Platform == DevicePlatform.Android
        ? "Not shown: MAUI's Image does not read Resources/Raw files on Android"
        : "Source assigned";

    private async void UpdateImageStatus()
    {
        var disk = await SkUiImageCache.GetDiskCacheBytesAsync();
        var cache = $"memory {SkUiImageCache.MemoryCacheCount} · {SkUiImageCache.MemoryCacheBytes / 1024} KiB, disk {disk / 1024} KiB";
        var loading = _skia.LoadingProgress switch
        {
            < 0 => "Loading (size unknown)",
            > 0 => $"Loading {_skia.LoadingProgress:P0}",
            _ => "Loading"
        };
        Feedback(_skia.IsLoading ? loading : _skia.LoadError is not null ? "Load failed: " + _skia.LoadError.Message
                : $"Image {_skia.ImageSize.Width:F0} x {_skia.ImageSize.Height:F0} {_lastLoad} · {cache}",
            _native.IsLoading ? "Loading" : _native.Source is null ? SampleEmpty : NativeStatus());
    }

    // The images are released when the page goes, not while a page covers it: the drawn pickers' lists open in a
    // modal popup page (a push). Clearing then, and loading the old sample again on return next to the new choice, left
    // MAUI's Image blank now and then (a pending load of the old source likely landing after the new one).
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if (args.NavigationType != NavigationType.Push)
        {
            _skia.Source = null;
            _native.Source = null;
            _core.SetSource(null);
        }
        base.OnNavigatedFrom(args);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_skia.Source is null) SetSample(_sample);
    }
}
