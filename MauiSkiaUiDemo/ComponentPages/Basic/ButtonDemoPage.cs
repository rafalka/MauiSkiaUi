using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiButton"/>, including an image beside the text (P9).</summary>
public sealed class ButtonDemoPage : ComponentDemoPage
{
    private const string NoImage = "None";
    private const string GlyphImage = "Glyph (FontImageSource)";
    private const string BadgeImage = "Badge (MauiImage)";
    private const string BotImage = "dotnet bot (scaled down)";
    private static readonly string[] ImageNames = [NoImage, GlyphImage, BadgeImage, BotImage];
    private static readonly Button.ButtonContentLayout.ImagePosition[] Positions =
        [Button.ButtonContentLayout.ImagePosition.Left, Button.ButtonContentLayout.ImagePosition.Top,
         Button.ButtonContentLayout.ImagePosition.Right, Button.ButtonContentLayout.ImagePosition.Bottom];

    private static ImageSource? CreateImage(string name) => name switch
    {
        GlyphImage => new FontImageSource { Glyph = "♥", FontFamily = DemoFonts.OpenSansRegular, Size = 22, Color = Colors.White },
        BadgeImage => ImageSource.FromFile(DemoAssets.BadgeImage),
        BotImage => ImageSource.FromFile(DemoAssets.DotnetBotImage),
        _ => null
    };

    private static string ImageName(ImageSource? source) => source switch
    {
        FontImageSource => GlyphImage,
        FileImageSource { File: DemoAssets.BadgeImage } => BadgeImage,
        FileImageSource { File: DemoAssets.DotnetBotImage } => BotImage,
        _ => NoImage
    };

    public ButtonDemoPage() : base(nameof(SkUiButton), new SkUiButton(), new Button())
    {
        var skia = (SkUiButton)SkiaControl;
        var native = (Button)NativeControl!;
        var skiaClicks = 0;
        var nativeClicks = 0;
        var canExecute = true;
        var skiaPress = "";
        var nativePress = "";
        void Counts() => Feedback($"Clicks: {skiaClicks}{skiaPress}", $"Clicks: {nativeClicks}{nativePress}");
        skia.Pressed += (_, _) => { skiaPress = " · pressed"; Counts(); };
        skia.Released += (_, _) => { skiaPress = " · released"; Counts(); };
        native.Pressed += (_, _) => { nativePress = " · pressed"; Counts(); };
        native.Released += (_, _) => { nativePress = " · released"; Counts(); };
        var skiaCommand = new Command(() => { skiaClicks++; Counts(); }, () => canExecute);
        var nativeCommand = new Command(() => { nativeClicks++; Counts(); }, () => canExecute);
        skia.Command = skiaCommand;
        native.Command = nativeCommand;
        native.TextColor = Colors.White;
        skia.TextColor = Colors.White;
        Text(nameof(SkUiButton.Text), "Add observation", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Choice(nameof(SkUiButton.LineBreakMode), [LineBreakMode.NoWrap, LineBreakMode.WordWrap, LineBreakMode.CharacterWrap, LineBreakMode.HeadTruncation, LineBreakMode.MiddleTruncation, LineBreakMode.TailTruncation],
            LineBreakMode.NoWrap, value => { skia.LineBreakMode = value; native.LineBreakMode = value; }, () => skia.LineBreakMode, () => native.LineBreakMode);
        Number(nameof(SkUiButton.FontSize), 10, 30, 16, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Number(nameof(SkUiButton.CornerRadius), 0, 30, 6, value => { skia.CornerRadius = (int)Math.Round(value); native.CornerRadius = (int)Math.Round(value); }, () => skia.CornerRadius, () => native.CornerRadius, whole: true);
        Number(nameof(SkUiButton.BorderWidth), 0, 8, 1, value => { skia.BorderWidth = value; native.BorderWidth = value; }, () => skia.BorderWidth, () => native.BorderWidth);
        ColorEditor(nameof(SkUiButton.FillColor), Accent, value => { skia.FillColor = value; native.Background = value; }, () => skia.FillColor, () => ((SolidColorBrush)native.Background).Color);
        ColorEditor(nameof(SkUiButton.BorderColor), Ink, value => { skia.BorderColor = value; native.BorderColor = value; }, () => skia.BorderColor, () => native.BorderColor);
        Choice(nameof(SkUiButton.ImageSource), ImageNames, NoImage, name => { skia.ImageSource = CreateImage(name); native.ImageSource = CreateImage(name); },
            () => ImageName(skia.ImageSource), () => ImageName(native.ImageSource));
        var position = Button.ButtonContentLayout.ImagePosition.Left;
        var spacing = 10d;
        void Layout()
        {
            skia.ContentLayout = new Button.ButtonContentLayout(position, spacing);
            native.ContentLayout = new Button.ButtonContentLayout(position, spacing);
        }
        Choice(nameof(SkUiButton.ContentLayout), Positions, position, value => { position = value; Layout(); },
            () => skia.ContentLayout.Position, () => native.ContentLayout.Position);
        Number("ContentLayout spacing", 0, 30, 10, value => { spacing = value; Layout(); }, () => skia.ContentLayout.Spacing, () => native.ContentLayout.Spacing);
        Toggle(nameof(Command.CanExecute), true, value => { canExecute = value; skiaCommand.ChangeCanExecute(); nativeCommand.ChangeCanExecute(); }, () => skia.Command.CanExecute(null), () => native.Command.CanExecute(null));
        OnReset(() => { skiaClicks = nativeClicks = 0; skiaPress = nativePress = ""; Counts(); });
    }
}
