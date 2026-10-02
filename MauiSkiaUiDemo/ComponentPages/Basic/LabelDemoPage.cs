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
        // For TextType = Html (as plain text it shows the markup).
        ["HTML"] = "<h3>Release notes</h3><p>Now with <b>bold</b>, <i>italic</i>, <u>underline</u>, <s>strike</s>, <span style=\"color:#C62828\">color</span> and <code>code</code>.</p><ul><li>One</li><li>Two with <a href=\"https://learn.microsoft.com/dotnet/maui/\">a link</a></li></ul>",
    };

    private const string NoLineBreaker = "None (LineBreakMode)";

    private static readonly Dictionary<string, SkUiTextLineBreaker?> LineBreakers = new()
    {
        [NoLineBreaker] = null,
        ["Ellipsis \"…\""] = SkUiTextLineBreakers.WithEllipsis("…"),
        ["Ellipsis \" (more)\""] = SkUiTextLineBreakers.WithEllipsis(" (more)"),
        // Whole words with a count of what was left out, e.g. "Earth is our home. +7 words".
        ["Words + count"] = context =>
        {
            var words = context.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (var shown = words.Length; shown > 0; shown--)
            {
                var candidate = string.Join(' ', words[..shown]) + (shown < words.Length ? $" +{words.Length - shown} words" : "");
                if (context.Fits(candidate))
                    return [candidate];
            }
            return context.Break();
        },
    };

    private const string NoFormattedText = "None (Text)";

    private static readonly string[] FormattedSamples = [NoFormattedText, "Styles", "Links", "Sizes", "Mixed RTL"];

    /// <summary>
    /// MAUI spans for <paramref name="sample"/>, built once per side (a formatted string belongs to one label); the
    /// tappable spans call <paramref name="tapped"/> with their text.
    /// </summary>
    private static FormattedString? Formatted(string sample, Action<string> tapped)
    {
        Span Link(string text)
        {
            var span = new Span { Text = text, TextColor = DemoColors.Accent, TextDecorations = TextDecorations.Underline };
            span.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => tapped(text)) });
            return span;
        }
        return sample switch
        {
            "Styles" => new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Red bold, ", TextColor = Colors.Red, FontAttributes = FontAttributes.Bold },
                    new Span { Text = "highlighted, ", BackgroundColor = Colors.Yellow },
                    new Span { Text = "struck through, ", TextDecorations = TextDecorations.Strikethrough },
                    new Span { Text = "spaced out, ", CharacterSpacing = 4 },
                    new Span { Text = "upper case, ", TextTransform = TextTransform.Uppercase },
                    new Span { Text = "italic and small.", FontAttributes = FontAttributes.Italic, FontSize = 12 }
                }
            },
            "Links" => new FormattedString
            {
                Spans = { new Span { Text = "By continuing you accept the " }, Link("terms of use"), new Span { Text = " and the " }, Link("privacy policy"), new Span { Text = "." } }
            },
            "Sizes" => new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Big words ", FontSize = 30, FontAttributes = FontAttributes.Bold },
                    new Span { Text = "and small ones wrap together in one paragraph; each line is as tall as its tallest span. " },
                    new Span { Text = "This span has its own line height.", LineHeight = 1.8, FontSize = 13 }
                }
            },
            "Mixed RTL" => new FormattedString
            {
                Spans = { new Span { Text = "Order " }, new Span { Text = "הזמנה 1234", FontAttributes = FontAttributes.Bold }, new Span { Text = " — " }, Link("مدفوع"), new Span { Text = " today." } }
            },
            _ => null
        };
    }

    private static string LineBreakerName(SkUiTextLineBreaker? breaker) =>
        LineBreakers.FirstOrDefault(entry => ReferenceEquals(entry.Value, breaker)).Key ?? NoLineBreaker;

    public LabelDemoPage() : base(nameof(SkUiLabel), new SkUiLabel(), new Label())
    {
        var skia = (SkUiLabel)SkiaControl;
        var native = (Label)NativeControl!;
        // With FormattedText set, Text is empty on both sides (as on MAUI): the editor's text waits for "None (Text)".
        Editor textEditor = null!;
        textEditor = MultilineText(nameof(SkUiLabel.Text), "Earth is our home.\nOceans cover 71 percent of its surface.", value => { skia.Text = value; native.Text = value; },
            () => skia.FormattedText is null ? skia.Text : textEditor.Text, () => native.FormattedText is null ? native.Text : textEditor.Text);
        Choice(nameof(SkUiLabel.FontFamily), new[] { DefaultFontFamily }.Concat(DemoFonts.RegisteredFamilies).ToArray(), DefaultFontFamily,
            value => { var family = value == DefaultFontFamily ? null : value; skia.FontFamily = family; native.FontFamily = family; },
            () => skia.FontFamily ?? DefaultFontFamily, () => native.FontFamily ?? DefaultFontFamily);
        Number(nameof(SkUiLabel.FontSize), 10, 36, 18, value => { skia.FontSize = value; native.FontSize = value; }, () => skia.FontSize, () => native.FontSize);
        Choice(nameof(SkUiLabel.LineBreakMode), Enum.GetValues<LineBreakMode>(), LineBreakMode.WordWrap, value => { skia.LineBreakMode = value; native.LineBreakMode = value; }, () => skia.LineBreakMode, () => native.LineBreakMode);
        // MaxLines: -1 / 0 is no limit; with TailTruncation the text wraps and the last line ends with the ellipsis.
        Number(nameof(SkUiLabel.MaxLines), -1, 6, -1, value => { skia.MaxLines = (int)value; native.MaxLines = (int)value; }, () => skia.MaxLines, () => native.MaxLines, whole: true);
        Number(nameof(SkUiLabel.LineHeight), 0.5, 3, 1, value => { skia.LineHeight = value; native.LineHeight = value; }, () => skia.LineHeight, () => native.LineHeight);
        Number(nameof(SkUiLabel.CharacterSpacing), -2, 10, 0, value => { skia.CharacterSpacing = value; native.CharacterSpacing = value; }, () => skia.CharacterSpacing, () => native.CharacterSpacing);
        Choice(nameof(SkUiLabel.TextDecorations), new[] { TextDecorations.None, TextDecorations.Underline, TextDecorations.Strikethrough, TextDecorations.Underline | TextDecorations.Strikethrough }, TextDecorations.None,
            value => { skia.TextDecorations = value; native.TextDecorations = value; }, () => skia.TextDecorations, () => native.TextDecorations);
        Choice(nameof(SkUiLabel.TextTransform), Enum.GetValues<TextTransform>(), TextTransform.Default, value => { skia.TextTransform = value; native.TextTransform = value; }, () => skia.TextTransform, () => native.TextTransform);
        // SkiaUi only: a custom line breaker (MAUI's Label has none, so the native side keeps its LineBreakMode).
        Choice(nameof(SkUiLabel.LineBreaker), LineBreakers.Keys.ToArray(), NoLineBreaker, value => skia.LineBreaker = LineBreakers[value], () => LineBreakerName(skia.LineBreaker));
        Choice(nameof(SkUiLabel.HorizontalTextAlignment), Enum.GetValues<TextAlignment>(), TextAlignment.Start, value => { skia.HorizontalTextAlignment = value; native.HorizontalTextAlignment = value; }, () => skia.HorizontalTextAlignment, () => native.HorizontalTextAlignment);
        Choice(nameof(SkUiLabel.FontAttributes), new[] { FontAttributes.None, FontAttributes.Bold, FontAttributes.Italic, FontAttributes.Bold | FontAttributes.Italic }, FontAttributes.None, value => { skia.FontAttributes = value; native.FontAttributes = value; }, () => skia.FontAttributes, () => native.FontAttributes);
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
        // HTML (MAUI's TextType): both labels parse Text; the drawn one raises LinkTapped for <a href>.
        skia.LinkTapped += (_, e) => Feedback($"Link tapped: {e.Href}");
        Choice(nameof(SkUiLabel.TextType), Enum.GetValues<TextType>(), TextType.Text, value => { skia.TextType = value; native.TextType = value; }, () => skia.TextType, () => native.TextType);
        // Spans (MAUI's FormattedText): both labels get the same spans; setting Text (None) clears them, as on MAUI.
        var formatted = NoFormattedText;
        var (skiaTaps, nativeTaps) = (0, 0);
        Choice(nameof(SkUiLabel.FormattedText), FormattedSamples, NoFormattedText, value =>
        {
            formatted = value;
            if (value == NoFormattedText)
            {
                skia.Text = textEditor.Text;
                native.Text = textEditor.Text;
                return;
            }
            skia.FormattedText = Formatted(value, text => Feedback($"Span tapped: {text} ({++skiaTaps})", $"Span tapped: {text} ({nativeTaps})"));
            native.FormattedText = Formatted(value, text => Feedback($"Span tapped: {text} ({skiaTaps})", $"Span tapped: {text} ({++nativeTaps})"));
        }, () => formatted, () => formatted);
        Number(nameof(SkUiLabel.Padding), 0, 24, 0, value => { skia.Padding = value; native.Padding = value; }, () => skia.Padding.Left, () => native.Padding.Left);
        // Badge / chip chrome without a wrapping border (MAUI's Label has none: the native side shows only the fill).
        ColorEditor(nameof(SkUiLabel.BackgroundColor), Colors.Transparent, value => { skia.BackgroundColor = value; native.BackgroundColor = value; }, () => skia.BackgroundColor, () => native.BackgroundColor);
        // Per-corner radii (CornerRadius sets all four at once; see the Button page).
        Number("CornerRadii.TopLeft", 0, 40, 0, value => { var r = skia.CornerRadii; skia.CornerRadii = new CornerRadius(value, r.TopRight, r.BottomLeft, r.BottomRight); }, () => skia.CornerRadii.TopLeft);
        Number("CornerRadii.TopRight", 0, 40, 0, value => { var r = skia.CornerRadii; skia.CornerRadii = new CornerRadius(r.TopLeft, value, r.BottomLeft, r.BottomRight); }, () => skia.CornerRadii.TopRight);
        Number("CornerRadii.BottomLeft", 0, 40, 0, value => { var r = skia.CornerRadii; skia.CornerRadii = new CornerRadius(r.TopLeft, r.TopRight, value, r.BottomRight); }, () => skia.CornerRadii.BottomLeft);
        Number("CornerRadii.BottomRight", 0, 40, 0, value => { var r = skia.CornerRadii; skia.CornerRadii = new CornerRadius(r.TopLeft, r.TopRight, r.BottomLeft, value); }, () => skia.CornerRadii.BottomRight);
        Number(nameof(SkUiLabel.BorderWidth), 0, 8, 0, value => skia.BorderWidth = value, () => skia.BorderWidth);
        ColorEditor(nameof(SkUiLabel.BorderColor), Ink, value => skia.BorderColor = value, () => skia.BorderColor);
    }
}
