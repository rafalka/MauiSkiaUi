using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: show the same images in many rows cheaply. Forty drawn rows show five different avatars: each avatar is
/// decoded once, small (<see cref="SkUiImage.DownsampleWidth"/>), cropped to a circle once
/// (<see cref="SkUiCircleTransformation"/>), and the result is shared by every row that shows it through SkiaUi's
/// memory cache (<see cref="SkUiImageCache"/>).
/// <para>
/// The avatars are generated into the temporary folder so the example works offline; with web URLs the downloads
/// would also be kept in the disk cache. The status line reads the cache: five entries for forty rows.
/// </para>
/// </summary>
public sealed class CachedAvatarsSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Cached avatars",
        Summary: "Forty rows, five avatars: each is decoded and cropped to a circle once, then shared by every row " +
            "through the image memory cache.",
        HowTo:
        [
            "Give each row an `SkUiImage` with the avatar's `Source` (a URL, a `MauiImage` name or a file).",
            "Decode it at the size you draw it: `DownsampleWidth=\"48\"` (DIPs at the display density).",
            "Add `<sk:SkUiCircleTransformation BorderWidth=\"2\" BorderColor=\"White\" />` to `Transformations`."
        ],
        ThingsToKnow:
        [
            "Rows with the same source, decode size and transformations share one decoded image; the second one shows " +
                "at once, without a decode.",
            "`SkUiImageCache.MemoryCacheMaxBytes` caps the cache; images still on screen stay alive when evicted.",
            "Downloads (`UriImageSource`) are kept on disk for `CacheValidity` (one day by default) unless " +
                "`CachingEnabled` is false.",
            "A stream source (`ImageSource.FromStream`) is not cached: it may return different bytes each time.",
            "URLs with an access token: set `SkUiImageCache.CacheKeyFactory = SkUiImageCacheKeys.IgnoreQueryParameters(\"token\")` " +
                "at startup so every token of one image shares its cache entries."
        ]);

    private static readonly (string Name, SKColor Color)[] People =
    [
        ("Ada", new SKColor(0x25, 0x63, 0xEB)), ("Grace", new SKColor(0xDB, 0x27, 0x77)), ("Linus", new SKColor(0x05, 0x96, 0x69)),
        ("Margaret", new SKColor(0xD9, 0x77, 0x06)), ("Alan", new SKColor(0x7C, 0x3A, 0xED))
    ];

    private readonly SkUiLabel _status = new() { FontSize = 13, TextColor = SampleColors.Caption, Margin = new Thickness(0, 0, 0, 8) };

    public CachedAvatarsSample() : base(Info) => SampleContent = Build();

    private View Build()
    {
        var files = People.Select(person => AvatarFile(person.Name, person.Color)).ToArray();
        var rows = new SkUiVerticalStackLayout { Spacing = 6, Padding = new Thickness(12) };
        rows.Children.Add(_status);
        for (var index = 0; index < 40; index++)
        {
            var person = index % People.Length;
            var avatar = new SkUiImage
            {
                Source = ImageSource.FromFile(files[person]),
                WidthRequest = 48,
                HeightRequest = 48,
                DownsampleWidth = 48
            };
            avatar.Transformations.Add(new SkUiCircleTransformation(2, Colors.White));
            avatar.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(SkUiImage.ImageSize)) UpdateStatus(); };
            var row = new SkUiHorizontalStackLayout { Spacing = 12 };
            row.Children.Add(avatar);
            row.Children.Add(new SkUiLabel { Text = $"{People[person].Name} · message {index + 1}", FontSize = 16, TextColor = SampleColors.Ink, VerticalOptions = LayoutOptions.Center });
            rows.Children.Add(row);
        }
        UpdateStatus();
        return new SkUiScrollView { Content = rows, HeightRequest = 520, BackgroundColor = SampleColors.Surface };
    }

    private void UpdateStatus() =>
        _status.Text = $"40 rows · {SkUiImageCache.MemoryCacheCount} images in the memory cache ({SkUiImageCache.MemoryCacheBytes / 1024} KiB)";

    /// <summary>A 512 px avatar (a colored disc with the initial) written once to the temporary folder.</summary>
    private static string AvatarFile(string name, SKColor color)
    {
        var path = Path.Combine(Path.GetTempPath(), $"skiaui-sample-avatar-{name}.png");
        if (File.Exists(path))
            return path;
        using var bitmap = new SKBitmap(512, 512);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
            using var font = new SKFont(SKTypeface.Default, 280);
            using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            canvas.DrawText(name[..1], 256, 256 + 100, SKTextAlign.Center, font, paint);
        }
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
