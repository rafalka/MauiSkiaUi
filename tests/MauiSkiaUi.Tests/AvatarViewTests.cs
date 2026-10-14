using MauiSkiaUi.Core;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// C8, <see cref="SkUiAvatarView"/> and <see cref="SkUiCoreAvatar"/>: the Community Toolkit's defaults and API, initials
/// from a name, colors by name, the image clipped to the stroke and decoded for the avatar, the badge, the same pixels on
/// both layers, and a new text re-recording only the avatar.
/// </summary>
[Collection(GlobalStateCollection.Name)] // the image cache
public sealed class AvatarViewTests : IDisposable
{
    private readonly IDisposable _font = SkUiTestHelpers.UseBundledFont();

    public AvatarViewTests() => SkUiImageCache.ClearMemoryCache();

    public void Dispose()
    {
        SkUiImageCache.ClearMemoryCache();
        _font.Dispose();
    }

    private static SKBitmap Render(Action<SKCanvas> paint, int width, int height)
    {
        var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        paint(canvas);
        return bitmap;
    }

    /// <summary>Renders <paramref name="view"/> at that size (avatars are centered at their own size otherwise).</summary>
    private static SKBitmap Render(SkUiView view, int width, int height)
    {
        view.WidthRequest = width;
        view.HeightRequest = height;
        SkUiTestHelpers.Arrange(view, width, height);
        return Render(view.Paint, width, height);
    }

    private static SKBitmap Render(SkUiCoreNode node, int width, int height)
    {
        node.SetWidth(width).SetHeight(height);
        node.Measure(width, height);
        node.Arrange(new Rect(0, 0, width, height));
        return Render(node.Paint, width, height);
    }

    private static byte[] ImageBytes(SKColor color, int width = 40, int height = 40)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static bool IsReddish(SKColor color) => color.Red > 200 && color.Green < 80 && color.Blue < 80;

    [Fact]
    public void DefaultsAreTheToolkits()
    {
        var avatar = new SkUiAvatarView();
        Assert.Equal("?", avatar.Text);
        Assert.Equal("?", avatar.DisplayText);
        Assert.Equal((Colors.White, 1d, -1d, Aspect.AspectFill), (avatar.BorderColor, avatar.BorderWidth, avatar.FontSize, avatar.Aspect));
        Assert.Equal((LayoutOptions.Center, LayoutOptions.Center), (avatar.HorizontalOptions, avatar.VerticalOptions));
        Assert.Equal(new Size(48, 48), ((IView)avatar).Measure(double.PositiveInfinity, double.PositiveInfinity));

        var core = new SkUiCoreAvatar();
        Assert.Equal(("?", new Thickness(1), 1d), (core.Text, core.Padding, core.StrokeThickness));
        Assert.Equal((LayoutAlignment.Center, LayoutAlignment.Center), (core.HorizontalAlignment, core.VerticalAlignment));
        Assert.Equal(new Size(48, 48), core.Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Theory]
    [InlineData("Ada Lovelace", SkUiAvatarInitials.FirstAndLastWord, "AL")]
    [InlineData("Ada Lovelace", SkUiAvatarInitials.FirstWord, "A")]
    [InlineData("  grace   brewster murray  hopper ", SkUiAvatarInitials.FirstAndLastWord, "GH")]
    [InlineData("Cher", SkUiAvatarInitials.FirstAndLastWord, "C")]
    [InlineData("émile zola", SkUiAvatarInitials.FirstAndLastWord, "ÉZ")]
    [InlineData("émile zola", SkUiAvatarInitials.FirstAndLastWord, "ÉZ")] // a combining accent stays with its letter
    [InlineData("😀 Bob (work)", SkUiAvatarInitials.FirstAndLastWord, "BW")] // words without a letter are skipped, leading punctuation too
    [InlineData("42 Street", SkUiAvatarInitials.FirstAndLastWord, "4S")]
    [InlineData("!!! ???", SkUiAvatarInitials.FirstAndLastWord, "")]
    [InlineData("   ", SkUiAvatarInitials.FirstAndLastWord, "")]
    [InlineData("Ada Lovelace", SkUiAvatarInitials.None, "")]
    public void InitialsComeFromTheFirstAndLastWords(string name, SkUiAvatarInitials type, string expected) =>
        Assert.Equal(expected, SkUiAvatarContent.Initials(name, type));

    [Fact]
    public void TextShowsAsItIsOrAsItsInitials()
    {
        var avatar = new SkUiAvatarView { Text = "ada lovelace", TextTransform = TextTransform.Uppercase };
        Assert.Equal("ADA LOVELACE", avatar.DisplayText); // None (default): as it is, as the toolkit's
        avatar.Initials = SkUiAvatarInitials.FirstAndLastWord;
        Assert.Equal("AL", avatar.DisplayText);
        avatar.Initials = SkUiAvatarInitials.FirstWord;
        Assert.Equal("A", avatar.DisplayText);
        avatar.Text = "?"; // no letter: as it is
        Assert.Equal("?", avatar.DisplayText);
        avatar.Initials = SkUiAvatarInitials.None;
        avatar.Text = "ab";
        Assert.Equal("AB", avatar.DisplayText);

        var core = new SkUiCoreAvatar().SetText("ada lovelace").SetInitials(SkUiAvatarInitials.FirstAndLastWord);
        Assert.Equal("AL", core.DisplayText);
        core.SetInitials(SkUiAvatarInitials.None);
        Assert.Equal("ada lovelace", core.DisplayText);
    }

    [Fact]
    public void ScreenReadersReadTheWholeText()
    {
        var avatar = new SkUiAvatarView { Text = "ZS" };
        var root = new SkUiVerticalStackLayout { Children = { avatar } };
        SkUiTestHelpers.Arrange(root, 100, 100);
        var info = SkUiSemanticsTree.Build(root).Flatten().Single(element => ReferenceEquals(element.Source, avatar));
        Assert.Equal((SkUiSemanticsRole.Image, "ZS"), (info.Role, info.Label));
        avatar.Text = "Zoe Saldana";
        avatar.Initials = SkUiAvatarInitials.FirstAndLastWord; // "ZS" drawn, the whole name read
        info = SkUiSemanticsTree.Build(root).Flatten().Single(element => ReferenceEquals(element.Source, avatar));
        Assert.Equal("Zoe Saldana", info.Label);
    }

    [Fact]
    public void ColorsByNameAreStableAndDifferBetweenNames()
    {
        var look = SkUiLook.Current;
        var ada = look.GetAvatarColors(SkUiAvatarColorMode.LightBackground, "Ada Lovelace");
        Assert.Equal(ada, look.GetAvatarColors(SkUiAvatarColorMode.LightBackground, " ada lovelace "));
        var names = new[] { "Ada Lovelace", "Grace Hopper", "Alan Turing", "Edsger Dijkstra", "Barbara Liskov", "Donald Knuth" };
        Assert.True(names.Select(name => look.GetAvatarColors(SkUiAvatarColorMode.DarkBackground, name).Background).Distinct().Count() > 2);
        Assert.Equal(SkUiLook.AvatarPaletteIndex("Ada Lovelace", 12), SkUiLook.AvatarPaletteIndex("ADA LOVELACE", 12));
        Assert.Equal(Colors.White, look.GetAvatarColors(SkUiAvatarColorMode.DarkBackground, "Ada").Text);
        Assert.Equal(new SkUiAvatarColors(SkUiColors.TrackOff, SkUiColors.DefaultForeground), look.GetAvatarColors(SkUiAvatarColorMode.Default, "Ada"));
    }

    [Fact]
    public void TheShapeIsFilledWithTheLooksColorUntilABackgroundIsSet()
    {
        var avatar = new SkUiAvatarView { Text = "" };
        using (var bitmap = Render(avatar, 48, 48))
        {
            Assert.Equal(SkUiShapePainter.ToSkColor(SkUiColors.TrackOff), bitmap.GetPixel(24, 24));
            Assert.True(bitmap.GetPixel(24, 0) is { Red: > 240, Green: > 240, Blue: > 240, Alpha: > 200 }); // the white border, inside the bounds
            Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha); // a circle: the corners are empty
        }
        avatar.AvatarColorMode = SkUiAvatarColorMode.DarkBackground;
        avatar.Text = "Ada Lovelace";
        avatar.Initials = SkUiAvatarInitials.FirstAndLastWord;
        using (var bitmap = Render(avatar, 48, 48))
            Assert.Equal(SkUiShapePainter.ToSkColor(SkUiLook.Current.GetAvatarColors(SkUiAvatarColorMode.DarkBackground, "Ada Lovelace").Background), bitmap.GetPixel(24, 40));
        avatar.BackgroundColor = Colors.Blue;
        using (var bitmap = Render(avatar, 48, 48))
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(24, 40));
        avatar.BackgroundColor = Colors.Transparent; // set: no default
        using (var bitmap = Render(avatar, 48, 48))
            Assert.Equal(0, bitmap.GetPixel(24, 40).Alpha);
    }

    [Fact]
    public void TheToolkitsNamesAndTheBorderMembersBothDrawTheOutline()
    {
        var avatar = new SkUiAvatarView { Text = "", BackgroundColor = Colors.Black, BorderColor = Colors.Red, BorderWidth = 6, CornerRadius = new CornerRadius(0) };
        using (var bitmap = Render(avatar, 48, 48))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel(0, 0)); // square corners
            Assert.Equal(SKColors.Red, bitmap.GetPixel(5, 24));
            Assert.Equal(SKColors.Black, bitmap.GetPixel(7, 24));
        }
        avatar.Stroke = Colors.Lime;
        avatar.StrokeThickness = 2;
        using (var bitmap = Render(avatar, 48, 48))
        {
            Assert.Equal(SKColors.Lime, bitmap.GetPixel(1, 24));
            Assert.Equal(SKColors.Black, bitmap.GetPixel(4, 24));
        }
        avatar.StrokeShape = new Ellipse();
        using (var bitmap = Render(avatar, 48, 48))
            Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
    }

    [Fact]
    public async Task TheImageFillsTheInsideOfTheStrokeOnceLoaded()
    {
        var avatar = new SkUiAvatarView
        {
            Text = "",
            ImageSource = ImageSource.FromStream(() => new MemoryStream(ImageBytes(SKColors.Red, 80, 40))),
            BorderColor = Colors.Blue,
            BorderWidth = 4,
        };
        await avatar.ImageLoadingTask;
        Assert.True(avatar.IsShowingImage);
        using var bitmap = Render(avatar, 60, 60);
        Assert.True(IsReddish(bitmap.GetPixel(30, 30)));
        Assert.True(IsReddish(bitmap.GetPixel(30, 7))); // aspect fill: the wide image covers the top of the circle
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(30, 1)); // the border over it
        Assert.Equal(0, bitmap.GetPixel(4, 4).Alpha); // clipped to the circle
    }

    [Fact]
    public async Task TheTextShowsWhileTheImageFails()
    {
        var avatar = new SkUiAvatarView
        {
            Text = "ZS",
            FontFamily = SkUiTestHelpers.BundledFontFamily,
            TextColor = Colors.Red,
            BackgroundColor = Colors.White,
            ImageSource = ImageSource.FromStream(() => new MemoryStream([1, 2, 3])),
        };
        await avatar.ImageLoadingTask;
        Assert.False(avatar.IsShowingImage);
        using var bitmap = Render(avatar, 48, 48);
        var red = 0;
        for (var x = 8; x < 40; x++)
            for (var y = 8; y < 40; y++)
                if (IsReddish(bitmap.GetPixel(x, y))) red++;
        Assert.True(red > 20, $"Expected the text in red, found {red} red pixels");
    }

    [Fact]
    public async Task TheImageIsDecodedToCoverTheAvatarAndSharedBetweenAvatars()
    {
        static ImageSource Face() => new StreamImageSource { Stream = _ => Task.FromResult<Stream>(new MemoryStream(ImageBytes(SKColors.Red, 800, 400))) };
        var key = SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(ImageBytes(SKColors.Red, 800, 400))), "face");
        var first = new SkUiCoreAvatar().SetImageSource(key);
        var second = new SkUiCoreAvatar().SetImageSource(key);
        await first.ImageLoadingTask;
        await second.ImageLoadingTask;
        Assert.Same(first.CachedImage, second.CachedImage); // a list of repeated faces decodes once
        var image = first.CachedImage!.Frames[0];
        var scale = SkUiImageLoader.DisplayScale;
        // 48 - 2 × (padding 1 + stroke 1) = 44 DIPs, covered: the image's height is 44, its width twice that.
        Assert.Equal(Math.Round(44 * scale), image.Height, 1);
        Assert.Equal(Math.Round(88 * scale), image.Width, 1);

        // Arranged larger: decoded again for the larger size.
        var avatar = new SkUiAvatarView { ImageSource = Face() };
        Assert.Equal(new Size(44, 44), avatar.ImageDecodeSize);
        await avatar.ImageLoadingTask;
        avatar.WidthRequest = avatar.HeightRequest = 100;
        SkUiTestHelpers.Arrange(avatar, 100, 100);
        Assert.Equal(new Size(96, 96), avatar.ImageDecodeSize);
        await avatar.ImageLoadingTask;
        Assert.True(avatar.IsShowingImage);
        avatar.WidthRequest = avatar.HeightRequest = 50;
        SkUiTestHelpers.Arrange(avatar, 50, 50); // smaller: kept
        Assert.Equal(new Size(96, 96), avatar.ImageDecodeSize);
    }

    [Fact]
    public void AutoSizedTextShrinksToTheAvatarAndASetSizeMeasures()
    {
        var avatar = new SkUiAvatarView { Text = "W", FontFamily = SkUiTestHelpers.BundledFontFamily };
        Assert.Equal(new Size(48, 48), ((IView)avatar).Measure(double.PositiveInfinity, double.PositiveInfinity));
        avatar.FontSize = 30;
        avatar.FontAutoScalingEnabled = false;
        var size = ((IView)avatar).Measure(double.PositiveInfinity, double.PositiveInfinity);
        Assert.True(size.Width < 48 && size.Height > 30, $"Measured {size}"); // one character of a 30-DIP font plus 2 × 2
        avatar.Text = "WWWW";
        Assert.True(((IView)avatar).Measure(double.PositiveInfinity, double.PositiveInfinity).Width > size.Width);

        // Sized by the look, long text shrinks to the circle instead of spilling out of it.
        var auto = new SkUiAvatarView { Text = "WWWWWWWW", FontFamily = SkUiTestHelpers.BundledFontFamily, TextColor = Colors.Red, BackgroundColor = Colors.White };
        using var bitmap = Render(auto, 48, 48);
        static bool Inked(SKColor color) => color.Red - color.Green > 40; // red text, antialiased over white
        Assert.False(Inked(bitmap.GetPixel(3, 24)));
        Assert.Contains(Enumerable.Range(4, 40).SelectMany(x => Enumerable.Range(20, 8).Select(y => bitmap.GetPixel(x, y))), Inked);
    }

    [Fact]
    public void TheBadgeSitsOnTheOutlineOverTheBorder()
    {
        var avatar = new SkUiAvatarView { Text = "", BackgroundColor = Colors.Black, BadgeColor = Colors.Lime, BadgeBorderColor = Colors.Red };
        using (var bitmap = Render(avatar, 48, 48))
        {
            var bounds = SkUiLook.Current.GetAvatarBadgeBounds(new SKRect(0, 0, 48, 48), SkUiAvatarBadgePosition.BottomRight, 24);
            Assert.Equal(SKColors.Lime, bitmap.GetPixel((int)bounds.MidX, (int)bounds.MidY));
            Assert.Equal(SKColors.Red, bitmap.GetPixel((int)bounds.MidX, (int)bounds.Top + 1)); // the ring
            Assert.True(bounds.Right <= 48 && bounds.Bottom <= 48);
            Assert.True(bounds.Left > 24 && bounds.Top > 24);
        }
        avatar.BadgePosition = SkUiAvatarBadgePosition.TopLeft;
        using (var bitmap = Render(avatar, 48, 48))
        {
            var bounds = SkUiLook.Current.GetAvatarBadgeBounds(new SKRect(0, 0, 48, 48), SkUiAvatarBadgePosition.TopLeft, 24);
            Assert.Equal(SKColors.Lime, bitmap.GetPixel((int)bounds.MidX, (int)bounds.MidY));
        }
        avatar.BadgeColor = null;
        using (var bitmap = Render(avatar, 48, 48))
            Assert.NotEqual(SKColors.Lime, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public void ABadgePainterOrAnOverrideDrawsTheBadge()
    {
        // A painter alone shows a badge (no BadgeColor): a pill of its own at the top right, outside the look's dot bounds.
        static void Pill(SKCanvas canvas, SkUiAvatarBadgePaint paint)
        {
            using var fill = new SKPaint { Color = SKColors.Red, IsAntialias = false };
            canvas.DrawRect(new SKRect(paint.Avatar.Right - 30, 0, paint.Avatar.Right, 14), fill);
        }
        var avatar = new SkUiAvatarView { Text = "", BackgroundColor = Colors.Black, BadgePosition = SkUiAvatarBadgePosition.TopRight }.SetBadgePainter(Pill);
        var core = new SkUiCoreAvatar().SetText("").SetBackgroundColor(Colors.Black).SetBadge(null, SkUiAvatarBadgePosition.TopRight).SetBadgePainter(Pill);
        using (var bitmap = Render(avatar, 48, 48))
        {
            Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 7));
            Assert.NotEqual(SKColors.Red, bitmap.GetPixel(40, 40));
        }
        using (var expected = Render(avatar, 48, 48))
        using (var actual = Render(core, 48, 48))
            Assert.Equal(expected.Pixels, actual.Pixels);

        // The painter gets the badge color and corner; without a painter or a color there is no badge.
        SkUiAvatarBadgePaint? seen = null;
        avatar.BadgeColor = Colors.Lime;
        avatar.BadgePainter = (_, paint) => seen = paint;
        Render(avatar, 48, 48).Dispose();
        Assert.Equal((SKColors.Lime, SkUiAvatarBadgePosition.TopRight, new SKRect(0, 0, 48, 48)), (seen!.Value.Color, seen.Value.Position, seen.Value.Avatar));
        avatar.BadgePainter = null;
        avatar.BadgeColor = null;
        using (var bitmap = Render(avatar, 48, 48))
            Assert.Equal(SKColors.Black, bitmap.GetPixel(40, 8));

        // A subclass draws its own badge.
        using (var bitmap = Render(new SquareBadgeAvatar { Text = "", BackgroundColor = Colors.Black }, 48, 48))
            Assert.Equal(SKColors.Blue, bitmap.GetPixel(44, 44));
    }

    private sealed class SquareBadgeAvatar : SkUiAvatarView
    {
        protected override bool HasBadge => true;

        protected override void OnPaintBadge(SKCanvas canvas, SkUiAvatarBadgePaint paint)
        {
            using var fill = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(paint.Bounds, fill);
        }
    }

    [Fact]
    public async Task BothLayersDrawTheSamePixels()
    {
        AssertSamePixels(
            new SkUiAvatarView { Text = "Ada Lovelace", Initials = SkUiAvatarInitials.FirstAndLastWord, AvatarColorMode = SkUiAvatarColorMode.DarkBackground, FontFamily = SkUiTestHelpers.BundledFontFamily, BadgeColor = Colors.Lime },
            new SkUiCoreAvatar().SetText("Ada Lovelace").SetInitials(SkUiAvatarInitials.FirstAndLastWord).SetAvatarColorMode(SkUiAvatarColorMode.DarkBackground).SetFont(SkUiTestHelpers.BundledFontFamily).SetBadge(Colors.Lime));
        AssertSamePixels(
            new SkUiAvatarView { Text = "zs", TextTransform = TextTransform.Uppercase, FontFamily = SkUiTestHelpers.BundledFontFamily, FontSize = 14, BackgroundColor = Colors.Navy, CornerRadius = new CornerRadius(8), StrokeThickness = 3 },
            new SkUiCoreAvatar().SetText("zs").SetTextTransform(TextTransform.Uppercase).SetFont(SkUiTestHelpers.BundledFontFamily, 14)
                .SetBackgroundColor(Colors.Navy).SetCornerRadius(8).SetStrokeThickness(3));

        var bytes = ImageBytes(SKColors.Red, 60, 30);
        var view = new SkUiAvatarView { ImageSource = ImageSource.FromStream(() => new MemoryStream(bytes)) };
        var core = new SkUiCoreAvatar().SetImageSource(SkUiImageSource.FromStream(_ => Task.FromResult<Stream>(new MemoryStream(bytes))));
        await view.ImageLoadingTask;
        await core.ImageLoadingTask;
        AssertSamePixels(view, core);

        static void AssertSamePixels(SkUiView view, SkUiCoreNode node)
        {
            using var expected = Render(view, 56, 56);
            using var actual = Render(node, 56, 56);
            Assert.NotEqual(0, expected.GetPixel(28, 28).Alpha); // something was drawn
            Assert.Equal(expected.Pixels, actual.Pixels);
        }
    }

    [Fact]
    public void ANewTextRerecordsOnlyTheAvatar()
    {
        var layout = new SkUiVerticalStackLayout();
        var avatar = new SkUiAvatarView { Text = "Ada Lovelace", Initials = SkUiAvatarInitials.FirstAndLastWord };
        var label = new SkUiLabel { Text = "Ada" };
        layout.Children.Add(avatar);
        layout.Children.Add(label);
        using var surface = new SkUiTestSurface(layout, 100, 120);
        surface.Frame(0);
        var recorded = surface.RecordedPictures;
        avatar.InvalidatePaint();
        surface.Frame(16);
        var avatarOnly = surface.RecordedPictures - recorded; // its content and its overlay (the border)
        recorded = surface.RecordedPictures;
        avatar.Text = "Grace Hopper";
        avatar.Text = "Grace Holt"; // the same initials: still repainted (the colors follow the whole text)
        surface.Frame(32);
        Assert.Equal(avatarOnly, surface.RecordedPictures - recorded);
        Assert.Equal(new Rect(26, 0, 48, 48), avatar.Frame); // no layout change
    }

    [Fact]
    public void FluentSettersValidate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiAvatarView().SetFont(size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiCoreAvatar().SetFont(size: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SkUiAvatarView().SetBorderWidth(-1));
        var avatar = new SkUiAvatarView { FontSize = 12 };
        try { avatar.FontSize = 0; } catch (ArgumentException) { } // MAUI ignores (or rejects) an invalid value
        Assert.Equal(12, avatar.FontSize);
    }

    [Fact]
    public void ACornerRadiusEqualToItsDefaultValueStillMakesItSquare()
    {
        // MAUI raises no change for CornerRadius = 0 (the property's default), but the fully rounded default gives way.
        var avatar = new SkUiAvatarView { BackgroundColor = Colors.Red, BorderWidth = 0 };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = avatar, Background = Colors.White }, 48, 48);
        Assert.Equal(SKColors.White, surface.Frame().GetPixel(3, 3)); // a circle
        avatar.CornerRadius = new CornerRadius(0);
        Assert.Equal(SKColors.Red, surface.Frame().GetPixel(3, 3)); // a square
        avatar.ClearValue(SkUiBorder.CornerRadiusProperty);
        Assert.Equal(SKColors.White, surface.Frame().GetPixel(3, 3)); // round again
    }

    [Fact]
    public void ACoreAvatarRepaintsWhenItsBackgroundIsSetToTheFieldsDefault()
    {
        var avatar = new SkUiCoreAvatar { Text = "Ada Lovelace" };
        using var surface = new SkUiTestSurface(new SkUiContentView { Content = new SkUiCoreHost().SetContent(avatar), Background = Colors.White }, 48, 48);
        Assert.NotEqual(SKColors.White, surface.Frame().GetPixel(24, 40)); // the look's color
        avatar.SetBackgroundColor(Colors.Transparent); // equal to the field's default: still replaces the look's fill
        Assert.Equal(SKColors.White, surface.Frame().GetPixel(24, 40));
    }
}
