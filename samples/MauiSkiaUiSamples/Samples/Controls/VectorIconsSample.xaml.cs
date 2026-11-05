using MauiSkiaUi;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>A settings row: its icon file, title and value.</summary>
public sealed record IconRow(string Icon, string Title, string Value);

/// <summary>
/// HOWTO: icons from <c>.svg</c> files, drawn at runtime. The page's XAML (VectorIconsSample.xaml) shows
/// <see cref="SkUiVectorImageSource"/> in a list's rows with the file from a binding, in a button and in image buttons, a file
/// with a color of its own, and one file in three sizes. Its <c>Color</c> uses <c>AppThemeBinding</c>, so the switch at the top
/// recolors every icon: each is drawn again, and the old one stays shown until then.
/// </summary>
public sealed partial class VectorIconsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Icons from SVG files",
        Summary: "SVG icons drawn at runtime: sharp at any size, colored by the theme, from one small file instead of a set " +
                 "of PNGs or an icon font.",
        HowTo:
        [
            "Ship the files as assets: `<MauiAsset Include=\"Resources/Raw/**\" LogicalName=\"%(RecursiveDir)%(Filename)%(Extension)\" />`. " +
                "Keep them out of `Resources/Images`, which turns them into PNGs at build time.",
            "Draw the parts to recolor in `currentColor`: `fill=\"currentColor\"` or `stroke=\"currentColor\"`.",
            "Show one: `<sk:SkUiVectorImageSource Source=\"icons/bell.svg\" Color=\"{AppThemeBinding Light=Black, Dark=White}\" Size=\"24\" />` " +
                "in any image property: `SkUiImage.Source`, `SkUiButton.ImageSource`, `SkUiImageButton.Source`, `SkUiSlider.ThumbImageSource`.",
            "Or without a color: `<sk:SkUiImage Source=\"icons/bell.svg\" />`; `currentColor` is black then.",
            "On Core nodes: `image.SetSource(SkUiImageSource.FromVector(\"icons/bell.svg\", Colors.Teal, 24))`."
        ],
        ThingsToKnow:
        [
            "`Color` is the starting value of `currentColor`: colors written in the file stay, as the green in the status icon, and a `color` in the file wins for what is inside it.",
            "`SkUiImage` and `SkUiImageButton` draw the icon again in exact pixels at the size they show it; without a size of " +
                "its own, the view takes the file's size or `Size`.",
            "Gradients, `<use>`, `<pattern>`, filters and embedded images are not drawn: the load fails with a `LoadError` " +
                "naming what stopped it. Keep such files a `MauiImage`.",
            "A `MauiImage` of the same name wins over the asset, and then `Color` and `Size` do nothing.",
            "Once loaded, an icon costs what a PNG of its size does. Each new color or size is a new load, off the UI thread.",
            "`SkUiVectorImageSource` works on drawn controls only, not on MAUI's native `Image`."
        ]);

    public IReadOnlyList<IconRow> Rows { get; } =
    [
        new("icons/person.svg", "Account", "Ada"),
        new("icons/bell.svg", "Notifications", "On"),
        new("icons/lock.svg", "Privacy", "Friends")
    ];

    public VectorIconsSample() : base(Info)
    {
        InitializeComponent();
        BindingContext = this;
    }

    private void OnDarkThemeToggled(object? sender, ToggledEventArgs args) => SetTheme(args.Value ? AppTheme.Dark : AppTheme.Light);

    private static void SetTheme(AppTheme theme)
    {
        if (Application.Current is { } app)
            app.UserAppTheme = theme;
    }

    // The samples app is light: the theme is this page's only while it shows.
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        SetTheme(AppTheme.Light);
        base.OnNavigatedFrom(args);
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (DarkTheme.IsToggled)
            SetTheme(AppTheme.Dark);
    }
}
