using CommunityToolkit.Maui.Views;
using MauiSkiaUi;
using SkiaSharp;

namespace MauiSkiaUiDemo;

/// <summary>
/// Side-by-side property playground for <see cref="SkUiAvatarView"/> next to the Community Toolkit's <c>AvatarView</c>: the
/// toolkit's properties on both, then SkiaUi's extras (initials from a name, colors by name, a status badge) on the drawn
/// avatar only.
/// </summary>
public sealed class AvatarViewDemoPage : ComponentDemoPage
{
    private const string NoImage = "None";
    private const string PhotoImage = "Photo";
    private const string BotImage = "dotnet bot";
    private const string GlyphImage = "Font glyph";
    private const string BrokenImage = "Broken URI";
    private const string NoBadge = "None";
    private const string CountBadge = "Count (BadgePainter)";
    private static readonly string[] Images = [NoImage, PhotoImage, BotImage, GlyphImage, BrokenImage];
    private static readonly string[] Badges = [NoBadge, "Green", "Amber", "Red", CountBadge];

    public AvatarViewDemoPage() : base(nameof(SkUiAvatarView), new SkUiAvatarView(), new AvatarView(), widthRange: (24, 160, 48), heightRange: (24, 160, 48))
    {
        var skia = (SkUiAvatarView)SkiaControl;
        var native = (AvatarView)NativeControl!;
        string image = NoImage;
        string badge = NoBadge;
        // The actress who played Neytiri in Avatar (2009) is Zoe Saldana (the toolkit samples' "ZS").
        Text(nameof(SkUiAvatarView.Text), "ZS", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Choice(nameof(SkUiAvatarView.ImageSource), Images, NoImage, value =>
        {
            image = value;
            skia.ImageSource = Source(value);
            native.ImageSource = Source(value);
        }, () => image);
        ColorEditor(nameof(SkUiAvatarView.BorderColor), Colors.White, value => { skia.BorderColor = value; native.BorderColor = value; }, () => skia.BorderColor, () => native.BorderColor);
        Number(nameof(SkUiAvatarView.BorderWidth), 0, 8, 1, value => { skia.BorderWidth = value; native.BorderWidth = value; }, () => skia.BorderWidth, () => native.BorderWidth);
        Number(nameof(SkUiAvatarView.CornerRadius), 0, 80, 24, value => { skia.CornerRadius = new CornerRadius(value); native.CornerRadius = new CornerRadius(value); },
            () => skia.CornerRadius.TopLeft, () => native.CornerRadius.TopLeft);
        // -1 (default): the toolkit's default label size; SkiaUi sizes the text to the avatar.
        Choice(nameof(SkUiAvatarView.FontSize), [-1d, 12d, 16d, 24d], -1d, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Choice(nameof(SkUiAvatarView.FontAttributes), [FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic], FontAttributes.None,
            value => { skia.FontAttributes = value; native.FontAttributes = value; }, () => skia.FontAttributes, () => native.FontAttributes);
        Choice(nameof(SkUiAvatarView.TextTransform), [TextTransform.Default, TextTransform.Uppercase, TextTransform.Lowercase], TextTransform.Default,
            value => { skia.TextTransform = value; native.TextTransform = value; }, () => skia.TextTransform, () => native.TextTransform);

        // A solid fill (none set): SkiaUi's colors from AvatarColorMode, the toolkit's transparent background.
        EffectEditors(skia, native);

        // SkiaUi only.
        // Type a name into Text: "Zoe Saldana" shows "ZS".
        Choice(nameof(SkUiAvatarView.Initials), [SkUiAvatarInitials.None, SkUiAvatarInitials.FirstWord, SkUiAvatarInitials.FirstAndLastWord], SkUiAvatarInitials.None,
            value => skia.Initials = value, () => skia.Initials);
        Choice(nameof(SkUiAvatarView.AvatarColorMode), [SkUiAvatarColorMode.Default, SkUiAvatarColorMode.LightBackground, SkUiAvatarColorMode.DarkBackground], SkUiAvatarColorMode.Default,
            value => skia.AvatarColorMode = value, () => skia.AvatarColorMode);
        Choice(nameof(SkUiAvatarView.BadgeColor), Badges, NoBadge, value =>
        {
            badge = value;
            skia.BadgePainter = value == CountBadge ? DrawCount : null;
            skia.BadgeColor = value switch { "Green" => Colors.LimeGreen, "Amber" => Colors.Orange, "Red" => Colors.Red, _ => null };
        }, () => badge);
        Choice(nameof(SkUiAvatarView.BadgePosition),
            [SkUiAvatarBadgePosition.BottomRight, SkUiAvatarBadgePosition.TopRight, SkUiAvatarBadgePosition.BottomLeft, SkUiAvatarBadgePosition.TopLeft],
            SkUiAvatarBadgePosition.BottomRight, value => skia.BadgePosition = value, () => skia.BadgePosition);
        Choice(nameof(SkUiAvatarView.Aspect), [Aspect.AspectFill, Aspect.AspectFit, Aspect.Fill, Aspect.Center], Aspect.AspectFill,
            value => skia.Aspect = value, () => skia.Aspect);
    }

    /// <summary>A badge of the app's own: a red pill with a count, growing from the look's dot towards the avatar's middle.</summary>
    private static void DrawCount(SKCanvas canvas, SkUiAvatarBadgePaint paint)
    {
        const string count = "12";
        var height = Math.Max(16, paint.Bounds.Height);
        using var font = new SKFont(SKTypeface.Default, height * 0.65f);
        var width = Math.Max(height, font.MeasureText(count) + height * 0.6f);
        var right = paint.Position is SkUiAvatarBadgePosition.TopRight or SkUiAvatarBadgePosition.BottomRight;
        var x = right ? Math.Min(paint.Avatar.Right, paint.Bounds.MidX + width / 2) - width : Math.Max(paint.Avatar.Left, paint.Bounds.MidX - width / 2);
        var y = Math.Clamp(paint.Bounds.MidY - height / 2, paint.Avatar.Top, paint.Avatar.Bottom - height);
        var pill = new SKRect(x, y, x + width, y + height);
        using var fill = new SKPaint { IsAntialias = true, Color = paint.Ring };
        canvas.DrawRoundRect(pill, height / 2, height / 2, fill);
        fill.Color = SKColors.Red;
        pill.Inflate(-paint.RingWidth, -paint.RingWidth);
        canvas.DrawRoundRect(pill, pill.Height / 2, pill.Height / 2, fill);
        fill.Color = SKColors.White;
        font.MeasureText(count, out var bounds);
        canvas.DrawText(count, pill.MidX, pill.MidY - bounds.MidY, SKTextAlign.Center, font, fill);
    }

    private static ImageSource? Source(string name) => name switch
    {
        PhotoImage => ImageSource.FromFile(DemoAssets.EarthImage),
        BotImage => ImageSource.FromFile(DemoAssets.DotnetBotImage),
        GlyphImage => new FontImageSource { Glyph = "★", Color = Colors.Gold, Size = 32 },
        BrokenImage => ImageSource.FromUri(new Uri("https://localhost:1/missing.png")),
        _ => null
    };
}
