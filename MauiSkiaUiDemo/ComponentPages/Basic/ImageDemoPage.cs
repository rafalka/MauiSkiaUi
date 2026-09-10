using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiImage"/>.</summary>
public sealed class ImageDemoPage : ComponentDemoPage
{
    private readonly SkUiImage _skia;
    private readonly Image _native;
    private string _sample = SampleEarth;

    /// <summary>Valid package image sample.</summary>
    public const string SampleEarth = "Earth";
    /// <summary>Clears the image source.</summary>
    public const string SampleEmpty = "Empty";
    /// <summary>Feeds undecodable bytes to exercise load failure.</summary>
    public const string SampleInvalid = "Invalid";

    public ImageDemoPage() : base(nameof(SkUiImage), new SkUiImage(), new Image())
    {
        _skia = (SkUiImage)SkiaControl;
        _native = (Image)NativeControl!;
        Choice(nameof(SkUiImage.Aspect), new[] { Aspect.AspectFit, Aspect.AspectFill, Aspect.Fill }, Aspect.AspectFit,
            value => { _skia.Aspect = value; _native.Aspect = value; }, () => _skia.Aspect, () => _native.Aspect);
        Choice(nameof(SkUiImage.Source), new[] { SampleEarth, SampleEmpty, SampleInvalid }, SampleEarth, SetSample, () => _sample);
        _skia.PropertyChanged += (_, args) => { if (args.PropertyName is nameof(SkUiImage.IsLoading) or nameof(SkUiImage.LoadError)) UpdateImageStatus(); };
        _native.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(Image.IsLoading)) UpdateImageStatus(); };
        ActionButton("Reload image", () => SetSample(_sample));
        UpdateImageStatus();
    }

    private void SetSample(string value)
    {
        _sample = value;
        ImageSource? Source() => value == SampleEmpty ? null : new StreamImageSource
        {
            Stream = async token =>
            {
                if (value == SampleInvalid) return new MemoryStream([1, 2, 3]);
                using var stream = await FileSystem.Current.OpenAppPackageFileAsync(DemoAssets.EarthImage);
                var bytes = new MemoryStream();
                try { await stream.CopyToAsync(bytes, token); bytes.Position = 0; return bytes; }
                catch { bytes.Dispose(); throw; }
            }
        };
        _skia.Source = Source();
        _native.Source = Source();
    }

    private void UpdateImageStatus() => Feedback(_skia.IsLoading ? "Loading" : _skia.LoadError is not null ? "Decode failed" : $"Image {_skia.ImageSize.Width:F0} x {_skia.ImageSize.Height:F0}",
        _native.IsLoading ? "Loading" : _native.Source is null ? SampleEmpty : "Source assigned");

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
