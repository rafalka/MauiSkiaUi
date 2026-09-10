using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiLabel"/>.</summary>
public sealed class LabelDemoPage : ComponentDemoPage
{
    /// <summary>Sentinel for the system/default font family in the FontFamily picker.</summary>
    public const string DefaultFontFamily = "Default";

    public LabelDemoPage() : base(nameof(SkUiLabel), new SkUiLabel(), new Label())
    {
        var skia = (SkUiLabel)SkiaControl;
        var native = (Label)NativeControl!;
        MultilineText(nameof(SkUiLabel.Text), "Earth is our home.\nOceans cover 71 percent of its surface.", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Choice(nameof(SkUiLabel.FontFamily), new[] { DefaultFontFamily }.Concat(DemoFonts.RegisteredFamilies).ToArray(), DefaultFontFamily,
            value => { var family = value == DefaultFontFamily ? null : value; skia.FontFamily = family; native.FontFamily = family; },
            () => skia.FontFamily ?? DefaultFontFamily, () => native.FontFamily ?? DefaultFontFamily);
        Number(nameof(SkUiLabel.FontSize), 10, 36, 18, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Choice(nameof(SkUiLabel.LineBreakMode), Enum.GetValues<LineBreakMode>(), LineBreakMode.WordWrap, value => { skia.LineBreakMode = value; native.LineBreakMode = value; }, () => skia.LineBreakMode, () => native.LineBreakMode);
        Choice(nameof(SkUiLabel.HorizontalTextAlignment), Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Choice(nameof(SkUiLabel.FontAttributes), new[] { FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic }, FontAttributes.None, value => { skia.FontAttributes = value; native.FontAttributes = value; }, () => skia.FontAttributes, () => native.FontAttributes);
        ColorEditor(nameof(SkUiLabel.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor, () => native.TextColor);
        Choice(nameof(SkUiLabel.VerticalTextAlignment), Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.VerticalTextAlignment = value; native.VerticalTextAlignment = value; }, () => skia.VerticalTextAlignment, () => native.VerticalTextAlignment);
        Number(nameof(SkUiLabel.Padding), 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
    }
}
