using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiDemo;

/// <summary>
/// SVG icons drawn at runtime: <see cref="SkUiVectorImageSource"/>'s file, color and size, an icon following its view's size
/// (another aspect, then resize the preview area), in dark mode, in other image properties, on both layers, and a file the
/// reader rejects. The file editor applies to every icon on the page, and the color editor to all but the themed tile and the
/// white icon on the button.
/// </summary>
public sealed class VectorIconsDemoPage : ComponentDemoPage
{
    private const string FileEditor = "File";
    private const string Gear = "Gear";
    private const string Check = "Check";
    private const string Flag = "Flag";
    private const string Gradient = "Gradient (not supported)";
    private const double IconSize = 24;

    private static readonly Dictionary<string, string> Files = new()
    {
        [Gear] = DemoAssets.VectorGear,
        [Check] = DemoAssets.VectorCheck,
        [Flag] = DemoAssets.VectorFlag,
        [Gradient] = DemoAssets.VectorGradient
    };

    private static readonly double[] Sizes = [16, 24, 32, 48, 64, 96];

    private readonly SkUiImage _image;
    private readonly SkUiVectorImageSource _source = new() { Source = DemoAssets.VectorGear, Color = DemoColors.Accent };
    private readonly SkUiVectorImageSource _themed = new() { Source = DemoAssets.VectorGear };
    private readonly SkUiVectorImageSource _buttonIcon = new() { Source = DemoAssets.VectorGear, Color = Colors.White, Size = IconSize };
    private readonly List<SkUiVectorImageSource> _colored = [];
    private readonly SkUiCoreImage _core = new();
    private string _file = Gear;
    private bool _dark;

    public VectorIconsDemoPage() : base(nameof(SkUiImage), new SkUiImage(), widthRange: (16, 320, 96), heightRange: (16, 320, 96))
    {
        _image = (SkUiImage)SkiaControl;
        _image.Aspect = Aspect.Center; // draws the icon at its size, so the Size editor shows; the other aspects scale it to the view
        DocumentTypes(typeof(SkUiVectorImageSource));
        _image.LoadingFinished += (_, _) => UpdateStatus();
        _image.Source = _source;

        Choice(FileEditor, [.. Files.Keys], Gear, SetFile, () => _file);
        ColorEditor(nameof(SkUiVectorImageSource.Color), DemoColors.Accent, SetColor, () => _source.Color ?? Colors.Black);
        Number(nameof(SkUiVectorImageSource.Size), 0, 96, 0, value =>
        {
            _source.Size = value;
            ApplyToCore();
        }, () => _source.Size, whole: true);
        Choice(nameof(SkUiImage.Aspect), new[] { Aspect.Center, Aspect.AspectFit, Aspect.AspectFill, Aspect.Fill }, Aspect.Center,
            value => _image.Aspect = value, () => _image.Aspect);

        var sizes = new SkUiHorizontalStackLayout { Spacing = 8 };
        foreach (var size in Sizes)
            sizes.Children.Add(Icon(Colored(), size));
        AddEditor("Sizes", sizes, property: false);

        AddEditor("Button and slider thumb", new SkUiVerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new SkUiButton
                {
                    Text = "Settings", TextColor = Colors.White, Background = DemoColors.Accent, HorizontalOptions = LayoutOptions.Start,
                    ImageSource = _buttonIcon
                },
                new SkUiSlider
                {
                    Value = 0.5, HeightRequest = 32,
                    ThumbImageSource = Colored(IconSize)
                }
            }
        }, property: false);

        ApplyToCore();
        AddEditor("Both layers", new SkUiHorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Icon(_source, 48),
                new SkUiCoreHost { WidthRequest = 48, HeightRequest = 48 }.SetContent(_core)
            }
        }, property: false);

        Toggle("Dark mode", false, value =>
        {
            _dark = value;
            SetTheme(value ? AppTheme.Dark : AppTheme.Light);
        }, () => _dark);
        _themed.SetAppThemeColor(SkUiVectorImageSource.ColorProperty, Colors.Black, Colors.White);
        var tile = new SkUiContentView { WidthRequest = 72, HeightRequest = 72, HorizontalOptions = LayoutOptions.Start, Content = Icon(_themed, 48) };
        tile.SetAppThemeColor(BackgroundColorProperty, Colors.White, Colors.Black);
        AddEditor("Follows the theme", tile, property: false);

        UpdateStatus();
    }

    /// <summary>A source that follows the file and color editors.</summary>
    private SkUiVectorImageSource Colored(double size = 0)
    {
        var source = new SkUiVectorImageSource { Source = Files[_file], Color = DemoColors.Accent, Size = size };
        _colored.Add(source);
        return source;
    }

    private static SkUiImage Icon(SkUiVectorImageSource source, double size) => new()
    {
        Source = source,
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    private void SetFile(string value)
    {
        _file = value;
        var file = Files[value];
        _source.Source = file;
        _themed.Source = file;
        _buttonIcon.Source = file;
        foreach (var source in _colored)
            source.Source = file;
        ApplyToCore();
    }

    private void SetColor(Color value)
    {
        _source.Color = value;
        foreach (var source in _colored)
            source.Color = value;
        ApplyToCore();
    }

    private void ApplyToCore() => _core.SetSource(SkUiImageSource.FromVector(_source.Source!, _source.Color, _source.Size));

    private static void SetTheme(AppTheme theme)
    {
        if (Application.Current is { } app)
            app.UserAppTheme = theme;
    }

    private void UpdateStatus()
    {
        var error = _image.LoadError;
        Feedback(error is not null
            ? "LoadError: " + error.Message
            : $"{_image.ImageSize.Width:F0} x {_image.ImageSize.Height:F0} DIPs, drawn at {_image.CachedImage?.Frames[0].Width} x {_image.CachedImage?.Frames[0].Height} px");
    }

    // The demo's pages are light (App): the theme goes back when the page goes, not while a page covers it: the drawn pickers'
    // lists open in a modal popup page (a push).
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        if (args.NavigationType != NavigationType.Push)
            SetTheme(AppTheme.Light);
        base.OnNavigatedFrom(args);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (_dark)
            SetTheme(AppTheme.Dark);
    }
}
