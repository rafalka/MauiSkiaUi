using System.ComponentModel;
using MauiSkiaUi.Core;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// HTML text (<c>TextType="Html"</c>) on both layers: <see cref="SkUiHtml"/>'s tolerant parser (tags, styles, entities,
/// whitespace, blocks and lists), layout through the formatted-text engine as the equivalent spans, and links
/// (<c>LinkTapped</c>, <c>LinkTappedCommand</c>).
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class LabelHtmlTests
{
    private static readonly SKTypeface Mono = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "RobotoMono-Regular.ttf"));

    private static float Advance(double size = 16)
    {
        using var font = new SKFont(Mono, (float)size) { LinearMetrics = true };
        return font.MeasureText("0");
    }

    private static string Flat(string html) => string.Concat(SkUiHtml.Parse(html).Select(run => run.Text));

    /// <summary>The style of the run holding <paramref name="text"/>.</summary>
    private static SkUiHtmlStyle StyleOf(string html, string text) => SkUiHtml.Parse(html).Single(run => run.Text.Contains(text)).Style;

    private static SkUiLabel Html(string html) => new() { Text = html, TextType = TextType.Html, FontFamily = SkUiTestHelpers.BundledFontFamily };

    private static Size Measure(SkUiLabel label, double width) => ((IView)label).Measure(width, double.PositiveInfinity);

    private static void Tap(SkUiLabel label, double x, double y = 8)
    {
        label.Touch(new(1, SkUiTouchAction.Pressed, new Point(x, y)));
        label.Touch(new(1, SkUiTouchAction.Released, new Point(x, y)));
    }

    [Fact]
    public void TagsBecomeStyles()
    {
        const string html = "<b>bold</b> <i>italic</i> <u>under</u> <s>struck</s> <b><i>both</i></b> <strong>s</strong><em>e</em>";
        Assert.Equal("bold italic under struck both se", Flat(html));
        Assert.Equal(FontAttributes.Bold, StyleOf(html, "bold").FontAttributes);
        Assert.Equal(FontAttributes.Italic, StyleOf(html, "italic").FontAttributes);
        Assert.Equal(TextDecorations.Underline, StyleOf(html, "under").Decorations);
        Assert.Equal(TextDecorations.Strikethrough, StyleOf(html, "struck").Decorations);
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, StyleOf(html, "both").FontAttributes);

        Assert.Equal(1.5, StyleOf("<h1>Title</h1>", "Title").SizeScale);
        Assert.Equal(FontAttributes.Bold, StyleOf("<h1>Title</h1>", "Title").FontAttributes);
        Assert.Equal(0.8, StyleOf("<small>fine</small>", "fine").SizeScale);
        Assert.Equal(SkUiHtml.MonospaceFamily, StyleOf("<code>x = 1</code>", "x = 1").FontFamily);
        var font = StyleOf("<font color=\"#FF0000\" face='Serif' size=5>font</font>", "font");
        Assert.Equal(Colors.Red, font.TextColor);
        Assert.Equal("Serif", font.FontFamily);
        Assert.Equal(1.5, font.SizeScale);
    }

    [Fact]
    public void InlineStylesAreRead()
    {
        var style = StyleOf("<span style=\"color: blue; background-color:#ffff00; font-weight: 700; font-style: italic; text-decoration: underline line-through; font-size: 20px\">css</span>", "css");
        Assert.Equal(Colors.Blue, style.TextColor);
        Assert.Equal(Color.FromArgb("#ffff00"), style.Background);
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, style.FontAttributes);
        Assert.Equal(TextDecorations.Underline | TextDecorations.Strikethrough, style.Decorations);
        Assert.Equal(20, style.FontSize);
        Assert.Equal(1.5, StyleOf("<span style='font-size:150%'>p</span>", "p").SizeScale);
        Assert.Equal(16, StyleOf("<span style='font-size:12pt'>p</span>", "p").FontSize);
        Assert.Equal(FontAttributes.None, StyleOf("<b><span style='font-weight:normal'>n</span></b>", "n").FontAttributes);
        var link = StyleOf("<a href='x' style='text-decoration:none'>plain link</a>", "plain link");
        Assert.Equal(TextDecorations.None, link.Decorations);
        Assert.Equal("x", link.Href);
    }

    [Fact]
    public void CssResetsOverrideTheLabelsDefaults()
    {
        var runs = SkUiHtml.Parse("<span style='font-weight:normal; text-decoration:none'>plain</span> <i>italic</i>");
        var text = SkUiHtml.ToRichText(runs, null, 16, FontAttributes.Bold, 0, -1, Colors.Black, TextDecorations.Underline);
        Assert.Equal(FontAttributes.None, text.Styles[0].FontAttributes); // the label is bold: the markup turns it off
        Assert.Equal(TextDecorations.None, text.Paints[0].Decorations);
        Assert.Equal(FontAttributes.Bold, text.Styles[1].FontAttributes); // " " keeps the label's
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, text.Styles[2].FontAttributes);
        Assert.Equal(TextDecorations.Underline, text.Paints[2].Decorations);
        // Spans for the label carry the reset too.
        var span = SkUiHtml.ToFormattedString("<span style='font-weight:normal'>x</span>").Spans[0];
        Assert.True(span.IsSet(Span.FontAttributesProperty));
        Assert.Equal(FontAttributes.None, span.FontAttributes);
    }

    [Fact]
    public void WhitespaceCollapsesAndEntitiesDecode()
    {
        Assert.Equal("a b c", Flat("  a \n\t b   <b> c </b>  "));
        Assert.Equal("Tom & Jerry <3  \U0001F600", Flat("Tom &amp; Jerry &lt;3 &nbsp;&#x1F600;"));
        Assert.Equal("keep  two\n spaces", Flat("<pre>keep  two\n spaces</pre>"));
        Assert.Equal("a\nb", Flat("a<br>b"));
        Assert.Equal("a\n\nb", Flat("a<br/><br />b"));
        Assert.Equal("shown", Flat("<script>hidden()</script><style>p{}</style><!-- note -->shown"));
        Assert.Equal("a < b", Flat("a < b"));
    }

    [Fact]
    public void BlocksAndListsArePararaphs()
    {
        Assert.Equal("One\nTwo\nThree", Flat("<p>One</p><p>Two</p><div>Three</div>"));
        Assert.Equal("Title\nBody", Flat("<h2>Title</h2>Body"));
        Assert.Equal("a\nb", Flat("<p>a<br></p><p>b</p>")); // a <br> at the end of a block adds no blank line
        Assert.Equal("Items:\n• one\n• two", Flat("Items:<ul><li>one</li><li>two</li></ul>"));
        Assert.Equal("1. one\n2. two\n • nested", Flat("<ol><li>one<li>two<ul><li>nested</li></ul></ol>"));
        Assert.Equal("one\ntwo", Flat("<p>one<p>two")); // implied end tags
    }

    [Fact]
    public void BrokenMarkupNeverThrows()
    {
        Assert.Equal("bold and more", Flat("<b>bold and more")); // unclosed
        Assert.Equal(FontAttributes.Bold, StyleOf("<b>bold and more", "more").FontAttributes);
        Assert.Equal("text", Flat("</i>text</b>")); // stray end tags
        Assert.Equal("a <b", Flat("a <b")); // unterminated tag: text
        Assert.Equal("x", Flat("<b><i>x</b></i>")); // misnested
        Assert.Equal("kept", Flat("<unknown attr=1>kept</unknown>"));
        Assert.Empty(SkUiHtml.Parse(null));
        Assert.Empty(SkUiHtml.Parse("<img src='a.png'>"));
    }

    [Fact]
    public void HtmlMeasuresAsTheEquivalentSpansOnBothLayers()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        const string html = "<p>Plain <b>bold</b> <span style='font-size:32px'>big</span></p><p>second</p>";
        var label = Html(html);
        var spans = new SkUiLabel { FontFamily = SkUiTestHelpers.BundledFontFamily, FormattedText = SkUiHtml.ToFormattedString(html) };
        foreach (var width in new[] { double.PositiveInfinity, Advance() * 8 })
        {
            Assert.Equal(Measure(spans, width), Measure(label, width));
            var core = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTextType(TextType.Html).SetText(html);
            Assert.Equal(Measure(label, width), core.Measure(width, double.PositiveInfinity));
        }
        Assert.Equal(Advance() * 11 + Advance(32) * 3, Measure(label, double.PositiveInfinity).Width, 0.5);

        // The label's values are the defaults the markup scales and overrides.
        label.FontSize = 20;
        Assert.Equal(Advance(20) * 11 + Advance(32) * 3, Measure(label, double.PositiveInfinity).Width, 0.5);
        // Back to plain text: the markup shows as written.
        label.TextType = TextType.Text;
        Assert.Equal(Advance(20) * html.Length, Measure(label, double.PositiveInfinity).Width, 0.5);
        // FormattedText wins over HTML, as on MAUI.
        label.TextType = TextType.Html;
        label.FormattedText = new FormattedString { Spans = { new Span { Text = "abc" } } };
        Assert.Equal(Advance(20) * 3, Measure(label, double.PositiveInfinity).Width, 0.5);
    }

    [Fact]
    public void LinksRaiseLinkTappedAndTheCommand()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Html("Read <a href=\"https://example.com/?a=1&amp;b=2\">the docs</a> now");
        var events = new List<(object? Sender, string Href, Point Position)>();
        label.LinkTapped += (sender, e) => events.Add((sender, e.Href, e.Position));
        var commands = new List<object?>();
        label.LinkTappedCommand = new Command<object?>(commands.Add);
        var labelTaps = 0;
        label.Tapped += (_, _) => labelTaps++;
        SkUiTestHelpers.Arrange(label, 400, 40);

        Tap(label, Advance() * 7);
        var (sender, href, position) = Assert.Single(events);
        Assert.Same(label, sender);
        Assert.Equal("https://example.com/?a=1&b=2", href);
        Assert.Equal(new Point(Advance() * 7, 8), position);
        Assert.Equal([href], commands);
        Assert.Equal(0, labelTaps);

        Tap(label, Advance() * 2); // beside the link: the label's own tap
        Assert.Single(events);
        Assert.Equal(1, labelTaps);
        Assert.Equal(href, label.LinkAt(new Point(Advance() * 6, 8)));
        Assert.Null(label.LinkAt(new Point(Advance() * 2, 8)));
    }

    [Fact]
    public void LinksAreHitWhereRtlTextDrawsThem()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Html("<a href='first'>אבג</a> <a href='second'>דהו</a>");
        label.FlowDirection = FlowDirection.RightToLeft;
        var hrefs = new List<string>();
        label.LinkTapped += (_, e) => hrefs.Add(e.Href);
        SkUiTestHelpers.Arrange(label, 300, 40);
        var width = Measure(label, double.PositiveInfinity).Width;
        Tap(label, 300 - 2);
        Tap(label, 300 - width + 2);
        Assert.Equal(["first", "second"], hrefs);
    }

    [Fact]
    public void CoreLinksRaiseLinkTapped()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = new SkUiCoreLabel().SetFontFamily(SkUiTestHelpers.BundledFontFamily).SetTextType(TextType.Html).SetText("Go <a href='home'>home</a>");
        var hrefs = new List<string>();
        label.LinkTapped += (_, e) => hrefs.Add(e.Href);
        label.Measure(300, 40);
        label.Arrange(new Rect(0, 0, 300, 40));
        label.Touch(new(1, SkUiTouchAction.Pressed, new Point(Advance() * 4, 8)));
        label.Touch(new(1, SkUiTouchAction.Released, new Point(Advance() * 4, 8)));
        Assert.Equal(["home"], hrefs);
        Assert.Equal("home", label.LinkAt(new Point(Advance() * 4, 8)));

        var core = SkUiHtml.ToCoreSpans("<b>a</b><a href='x'>b</a>", linkTapped: hrefs.Add);
        Assert.Equal(FontAttributes.Bold, core[0].FontAttributes);
        Assert.Equal(TextDecorations.Underline, core[1].TextDecorations);
    }

    /// <summary>The MAUI docs' HTML label samples, with only the namespace prefix changed (plus the test font).</summary>
    [Fact]
    public void MauiDocSamplesLoadAndBind()
    {
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher();
        using var font = SkUiTestHelpers.UseBundledFont();
        var xaml = $$"""
            <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                         xmlns:sk="clr-namespace:MauiSkiaUi;assembly=MauiSkiaUi">
              <sk:SkUiVerticalStackLayout>
                <sk:SkUiLabel Text="This is &lt;strong style=&quot;color:red&quot;&gt;HTML&lt;/strong&gt; text."
                              TextType="Html" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
                <sk:SkUiLabel TextType="Html" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}">
                    <![CDATA[
                    This is <strong style="color:red">HTML</strong> text.
                    ]]>
                </sk:SkUiLabel>
                <sk:SkUiLabel Text="{Binding Html}" TextType="Html" FontFamily="{{SkUiTestHelpers.BundledFontFamily}}" />
              </sk:SkUiVerticalStackLayout>
            </ContentView>
            """;
        var root = new ContentView();
        Microsoft.Maui.Controls.Xaml.Extensions.LoadFromXaml(root, xaml);
        var labels = ((SkUiVerticalStackLayout)root.Content).Children.Cast<SkUiLabel>().ToArray();
        var model = new HtmlModel { Html = "<b>Ann</b>" };
        root.BindingContext = model;

        var expected = Advance() * "This is HTML text.".Length;
        Assert.Equal(expected, Measure(labels[0], double.PositiveInfinity).Width, 0.5);
        Assert.Equal(expected, Measure(labels[1], double.PositiveInfinity).Width, 0.5);
        Assert.Equal(Colors.Red, StyleOf(labels[0].Text, "HTML").TextColor);
        Assert.Equal(Advance() * 3, Measure(labels[2], double.PositiveInfinity).Width, 0.5);
        model.Html = "<i>Annabel</i>";
        Assert.Equal(Advance() * 7, Measure(labels[2], double.PositiveInfinity).Width, 0.5);
    }

    private sealed class HtmlModel : INotifyPropertyChanged
    {
        private string _html = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Html { get => _html; set { _html = value; PropertyChanged?.Invoke(this, new(nameof(Html))); } }
    }
}
