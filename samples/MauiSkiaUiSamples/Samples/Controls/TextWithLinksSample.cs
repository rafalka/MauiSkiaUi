using MauiSkiaUi;
using MauiSkiaUi.Core;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>
/// HOWTO: mix styles in one label and make parts of it tappable. <see cref="SkUiLabel.FormattedText"/> takes MAUI's own
/// <see cref="FormattedString"/> and <see cref="Span"/>s, so the XAML of a MAUI page ports by changing the label's prefix:
/// each span sets the font, size, attributes, colors, decorations, character spacing or line height it needs, and leaves
/// the rest to the label. Spans wrap as one paragraph, and a span's <see cref="TapGestureRecognizer"/> runs when that span
/// is tapped.
/// <para>
/// The page shows a consent line with two links, a notification row (bold name, muted time) and the same links on the
/// Core layer (<see cref="SkUiCoreSpan"/> with a <c>Tapped</c> handler). Tapping a link updates the caption at the top.
/// </para>
/// </summary>
public sealed class TextWithLinksSample : SamplePage, ISample
{
    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "Text with links",
        Summary: "One label, several styles: `FormattedText` with MAUI's `Span`s, some of them tappable like links. " +
            "Tap a link to see which one it was.",
        HowTo:
        [
            "Set `label.FormattedText = new FormattedString { Spans = { new Span { Text = \"Hello \" }, … } }` (in XAML: " +
                "`<sk:SkUiLabel.FormattedText>` with a `FormattedString` inside, as on MAUI's Label).",
            "Style a span with `TextColor`, `BackgroundColor`, `FontSize`, `FontAttributes`, `TextDecorations`, " +
                "`CharacterSpacing`, `LineHeight` or `TextTransform`. What a span does not set is the label's.",
            "Make a span tappable: `span.GestureRecognizers.Add(new TapGestureRecognizer { Command = … })`. Its `Tapped` " +
                "event works too.",
            "Core labels: `coreLabel.SetSpans(new SkUiCoreSpan(\"text\").SetTextColor(…), …)`, and `span.Tapped += …`.",
            "HTML instead: `label.TextType = TextType.Html` with markup in `Text`; links raise `LinkTapped` (and " +
                "`LinkTappedCommand`) with their `href`."
        ],
        ThingsToKnow:
        [
            "Setting `Text` clears `FormattedText` and the other way round, as on MAUI.",
            "A press on a tappable span takes the tap from the label and its parents; a press beside it does not, so a " +
                "card's own tap keeps working around the links.",
            "The label's `LineBreakMode`, `MaxLines`, alignment and padding apply to spans. `LineBreaker` and " +
                "`TextRendering` do not: spans are always shaped.",
            "A line is as tall as its tallest span. Changing a span's color or decorations only repaints."
        ]);

    public TextWithLinksSample() : base(Info) => SampleContent = Build();

    private static View Build()
    {
        var caption = new Label { Text = "Tap a link", FontSize = 13, TextColor = SampleColors.Caption };
        void Opened(string what) => caption.Text = $"Opened: {what}";

        Span Link(string text)
        {
            var link = new Span { Text = text, TextColor = SampleColors.Accent, TextDecorations = TextDecorations.Underline };
            link.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Opened(text)) });
            return link;
        }

        var consent = new SkUiLabel
        {
            FontSize = 16,
            TextColor = SampleColors.Ink,
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "By creating an account you accept the " },
                    Link("terms of use"),
                    new Span { Text = " and confirm that you have read the " },
                    Link("privacy policy"),
                    new Span { Text = "." }
                }
            }
        };

        var notification = new SkUiLabel
        {
            FontSize = 15,
            TextColor = SampleColors.Ink,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "Margaret Hamilton", FontAttributes = FontAttributes.Bold },
                    new Span { Text = " commented on " },
                    new Span { Text = "Apollo guidance", FontAttributes = FontAttributes.Italic },
                    new Span { Text = ": \"Priority displays saved the landing.\"" },
                    new Span { Text = "  2 h", FontSize = 12, TextColor = SampleColors.Caption }
                }
            }
        };

        // The same kind of text from HTML: SkiaUi parses the markup (same result on every platform).
        var html = new SkUiLabel
        {
            FontSize = 15,
            TextColor = SampleColors.Ink,
            TextType = TextType.Html,
            Text = "From HTML: <b>bold</b>, <i>italic</i>, <span style=\"background-color:#FFF59D\">marked</span> and " +
                "<a href=\"https://learn.microsoft.com/dotnet/maui/\">a link</a>."
        };
        html.LinkTapped += (_, e) => Opened(e.Href);

        // The same links on the Core layer: plain spans with Tapped handlers.
        var coreLink = new SkUiCoreSpan("help centre").SetTextColor(SampleColors.Accent).SetTextDecorations(TextDecorations.Underline);
        coreLink.Tapped += (_, _) => Opened("help centre (Core)");
        var core = new SkUiCoreLabel()
            .SetSpans(new SkUiCoreSpan("Core label: questions? Visit the "), coreLink, new SkUiCoreSpan("."))
            .SetFontSize(15)
            .SetTextColor(SampleColors.Ink);

        var column = new SkUiVerticalStackLayout
        {
            Spacing = 16,
            Padding = new Thickness(12),
            Children = { consent, notification, html, new SkUiCoreHost().SetContent(core) }
        };
        var surface = new SkUiContentView { Content = column, BackgroundColor = SampleColors.Surface };
        return new VerticalStackLayout { Spacing = 8, Children = { caption, surface } };
    }
}
