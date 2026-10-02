using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side property playground for <see cref="SkUiImage"/>: every source kind (raw asset, MauiImage PNG and SVG,
/// font glyph, web, animated GIF, stream), animation, SkiaUi's transformations and downsampling, and the shared caches.
/// </summary>
public sealed class ImageDemoPage : ComponentDemoPage
{
    private readonly SkUiImage _skia;
    private readonly Image _native;
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
        Choice(nameof(SkUiImage.Source), new[] { SampleEarth, SampleMauiImage, SampleSvg, SampleGlyph, SampleWeb, SampleAnimated, SampleEmpty, SampleInvalid },
            SampleEarth, SetSample, () => _sample);
        Toggle(nameof(SkUiImage.IsAnimationPlaying), true,
            value => { _skia.IsAnimationPlaying = value; _native.IsAnimationPlaying = value; }, () => _skia.IsAnimationPlaying, () => _native.IsAnimationPlaying);
        Choice(nameof(SkUiImage.Transformations) + " (SkiaUi)", [.. TransformationChoices.Keys], TransformNone, SetTransformation, () => _transformation);
        Number(nameof(SkUiImage.DownsampleWidth) + " (SkiaUi)", 0, 300, 0, value => _skia.DownsampleWidth = value, () => _skia.DownsampleWidth, whole: true);
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
        _native.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Image.IsLoading)) UpdateImageStatus(); };
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
            SampleGlyph => new FontImageSource { Glyph = "♥", FontFamily = DemoFonts.OpenSansRegular, Size = 96, Color = DemoColors.Accent },
            SampleWeb => ImageSource.FromUri(new Uri(DemoAssets.WebImage)),
            SampleAnimated => ImageSource.FromStream(() => new MemoryStream(Spinner)),
            _ => ImageSource.FromStream(() => new MemoryStream([1, 2, 3]))
        };
        _skia.Source = Source();
        _native.Source = Source();
    }

    private async void UpdateImageStatus()
    {
        var disk = await SkUiImageCache.GetDiskCacheBytesAsync();
        var cache = $"memory {SkUiImageCache.MemoryCacheCount} · {SkUiImageCache.MemoryCacheBytes / 1024} KiB, disk {disk / 1024} KiB";
        Feedback(_skia.IsLoading ? "Loading" : _skia.LoadError is not null ? "Load failed: " + _skia.LoadError.Message
                : $"Image {_skia.ImageSize.Width:F0} x {_skia.ImageSize.Height:F0} {_lastLoad} · {cache}",
            _native.IsLoading ? "Loading" : _native.Source is null ? SampleEmpty : "Source assigned");
    }

    protected override void OnDisappearing()
    {
        _skia.Source = null;
        _native.Source = null;
        base.OnDisappearing();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_skia.Source is null) SetSample(_sample);
    }
}
