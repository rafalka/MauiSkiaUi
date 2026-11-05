using MauiSkiaUi;
using MauiSkiaUi.Markdown;

namespace MauiSkiaUiSamples.Samples.Controls;

/// <summary>HOWTO: show a Markdown document, such as release notes or an answer from a model, and decide what its links do.</summary>
public sealed class MarkdownDocumentSample : SamplePage, ISample
{
    private const string Document =
        "# Release notes\n\n" +
        "This version adds **formulas** and *zoom*. See the [changelog](https://example.com/changelog) or write to <support@example.com>.\n\n" +
        "> [!TIP]\n> Remote images stay off until you allow them.\n\n" +
        "## What changed\n\n" +
        "- [x] Tables scroll sideways when they are too wide\n- [x] Code blocks have a copy button\n- [ ] Text selection\n\n" +
        "| Platform | Status |\n|:---|:-:|\n| Android | Done |\n| iOS | Done |\n\n" +
        "```csharp\nview.LinkTapped += (_, e) => Open(e.Href);\n```\n\n" +
        "The area of a circle is $\\pi r^2$, and:\n\n$$\n\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}\n$$\n\n" +
        "An image from the web does not load here: ![a chart](https://example.com/chart.png)\n";

    private readonly SkUiLabel _status = new() { FontSize = 13, TextColor = SampleColors.Caption, Text = "Tap a link.", Margin = new Thickness(12, 0, 12, 12) };

    public static SampleInfo Info { get; } = new(
        SampleSection.Controls,
        Title: "A Markdown document",
        Summary: "Release notes drawn from Markdown, with no platform view per paragraph. Nothing a document says happens by itself: links raise an event, and remote images stay off.",
        HowTo:
        [
            "Add the `SkiaUi.Maui.Markdown` package, and `SkiaUi.Maui.Markdown.Math` for formulas (`UseSkiaUiMarkdownMath()` in `MauiProgram`).",
            "Put an `SkUiMarkdownView` in a scroll view and set its `Text`: it is as tall as its document.",
            "Handle `LinkTapped` or `LinkTappedCommand`, and check the target's scheme before you open it."
        ],
        ThingsToKnow:
        [
            "`AllowRemoteImages` is off by default: an image URL reaches its server as soon as it draws, which can track a reader or carry data out of a conversation.",
            "A document cannot read a local file: absolute paths and `file:` URLs show the alt text. `ImageResolver` replaces the rule.",
            "Set `Text` again as an answer grows: only the blocks that changed are built again, on a background thread.",
            "A document that is too deep or too large shows as plain text, with a `Warning`. Bad input never throws."
        ]);

    public MarkdownDocumentSample() : base(Info) => SampleContent = Build();

    private View Build()
    {
        var markdown = new SkUiMarkdownView { Text = Document, Margin = new Thickness(12) };
        markdown.LinkTapped += (_, args) => _status.Text = "Would open " + args.Href;
        var stack = new SkUiVerticalStackLayout { Spacing = 8, BackgroundColor = SampleColors.Surface };
        stack.Children.Add(markdown);
        stack.Children.Add(_status);
        return stack;
    }
}
