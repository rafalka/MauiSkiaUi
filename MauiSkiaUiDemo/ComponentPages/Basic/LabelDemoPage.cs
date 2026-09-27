using MauiSkiaUi;

namespace MauiSkiaUiDemo;

/// <summary>Side-by-side property playground for <see cref="SkUiLabel"/>.</summary>
public sealed class LabelDemoPage : ComponentDemoPage
{
    /// <summary>Sentinel for the system/default font family in the FontFamily picker.</summary>
    public const string DefaultFontFamily = "Default";

    private static readonly Dictionary<string, string> ScriptSamples = new()
    {
        ["Latin"] = "Earth is our home.\nOceans cover 71 percent of its surface.",
        ["Arabic"] = "مرحبا بالعالم. تغطي المحيطات ٧١٪ من سطح الأرض.",
        ["Hebrew"] = "שלום עולם! האוקיינוסים מכסים 71% משטח כדור הארץ.",
        ["Mixed LTR/RTL"] = "Order #1234 — הזמנה מספר 1234 (שולם) — مدفوع 50$ today.",
        ["Devanagari"] = "नमस्ते दुनिया। पृथ्वी हमारा घर है।",
        ["CJK"] = "地球は私たちの家です。海は表面の71％を覆っています。",
        ["Emoji"] = "Launch 🚀 ready 👍🏽 family 👩‍👩‍👧 flag 🇵🇱 done ✅",
    };

    public LabelDemoPage() : base(nameof(SkUiLabel), new SkUiLabel(), new Label())
    {
        var skia = (SkUiLabel)SkiaControl;
        var native = (Label)NativeControl!;
        var textEditor = MultilineText(nameof(SkUiLabel.Text), "Earth is our home.\nOceans cover 71 percent of its surface.", value => { skia.Text = value; native.Text = value; }, () => skia.Text, () => native.Text);
        Choice(nameof(SkUiLabel.FontFamily), new[] { DefaultFontFamily }.Concat(DemoFonts.RegisteredFamilies).ToArray(), DefaultFontFamily,
            value => { var family = value == DefaultFontFamily ? null : value; skia.FontFamily = family; native.FontFamily = family; },
            () => skia.FontFamily ?? DefaultFontFamily, () => native.FontFamily ?? DefaultFontFamily);
        Number(nameof(SkUiLabel.FontSize), 10, 36, 18, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Choice(nameof(SkUiLabel.LineBreakMode), Enum.GetValues<LineBreakMode>(), LineBreakMode.WordWrap, value => { skia.LineBreakMode = value; native.LineBreakMode = value; }, () => skia.LineBreakMode, () => native.LineBreakMode);
        Choice(nameof(SkUiLabel.HorizontalTextAlignment), Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Choice(nameof(SkUiLabel.FontAttributes), new[] { FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic }, FontAttributes.None, value => { skia.FontAttributes = value; native.FontAttributes = value; }, () => skia.FontAttributes, () => native.FontAttributes);
        ColorEditor(nameof(SkUiLabel.TextColor), Ink, value => { skia.TextColor = value; native.TextColor = value; }, () => skia.TextColor, () => native.TextColor);
        Choice(nameof(SkUiLabel.VerticalTextAlignment), Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.VerticalTextAlignment = value; native.VerticalTextAlignment = value; }, () => skia.VerticalTextAlignment, () => native.VerticalTextAlignment);
        // Script samples exercise HarfBuzz shaping, bidi and font fallback against the native Label.
        // They write through the Text editor so the Text property check stays consistent.
        var script = "Latin";
        Choice("Script", ScriptSamples.Keys.ToArray(), "Latin", value =>
        {
            script = value;
            textEditor.Text = ScriptSamples[value];
        }, () => script, () => script);
        Choice(nameof(SkUiLabel.FlowDirection), Enum.GetValues<FlowDirection>(), FlowDirection.MatchParent,
            value => { skia.FlowDirection = value; native.FlowDirection = value; }, () => skia.FlowDirection, () => native.FlowDirection);
        Number(nameof(SkUiLabel.Padding), 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
    }
}
