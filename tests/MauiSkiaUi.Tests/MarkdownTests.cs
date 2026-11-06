using MauiSkiaUi.Core;
using MauiSkiaUi.Markdown;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The Markdown view on Core nodes: what a document builds, updates and what bad input does. No network.</summary>
public class MarkdownTests
{
    private const double Width = 360;

    private static SkUiMarkdownStyle Style { get; } = SkUiMarkdownStyle.Default with
    {
        FontFamily = SkUiTestHelpers.BundledFontFamily,
        CodeFontFamily = SkUiTestHelpers.BundledFontFamily,
        MaximumInPlaceParseLength = int.MaxValue // what these tests check is what a document builds
    };

    /// <summary>What an app gets by default.</summary>
    private static SkUiMarkdownStyle Background { get; } = Style with { MaximumInPlaceParseLength = 0 };

    private static SkUiCoreMarkdown View(string text)
    {
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetText(text);
        Assert.False(view.IsPlainText);
        return view;
    }

    private static IEnumerable<T> Nodes<T>(ISkUiCoreNode root) where T : class
    {
        if (root is T match)
            yield return match;
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<ISkUiCoreNode>())
            foreach (var found in Nodes<T>(child))
                yield return found;
    }

    private static string AllText(ISkUiCoreNode root) => string.Join("\n", Nodes<SkUiCoreLabel>(root).Select(label =>
        label.Spans.Count > 0 ? string.Concat(label.Spans.Select(span => span.Text)) : label.Text));

    private static Size Draw(SkUiCoreNode view)
    {
        var size = view.Measure(Width, double.PositiveInfinity);
        view.Arrange(new Rect(0, 0, Width, size.Height));
        using var bitmap = new SKBitmap(32, 32);
        using var canvas = new SKCanvas(bitmap);
        view.Paint(canvas);
        return size;
    }

    private static SkUiCoreSpan SpanWith(ISkUiCoreNode root, string text) =>
        Nodes<SkUiCoreLabel>(root).SelectMany(label => label.Spans).First(span => span.Text == text);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void NoTextHasNoContent(string? text)
    {
        var view = new SkUiCoreMarkdown().SetText(text);
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
        Assert.Equal(0, view.Measure(Width, double.PositiveInfinity).Height);
    }

    [Fact]
    public void ChangingTheTextReplacesTheContent()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("first");
        Assert.Equal("first", AllText(view));
        view.SetText("second");
        Assert.Equal("second", AllText(view));
        view.SetText("");
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
    }

    [Fact]
    public void TextWrapsToTheWidth()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View(string.Join(" ", Enumerable.Repeat("word", 80)));
        var wide = view.Measure(1000, double.PositiveInfinity);
        var narrow = view.Measure(200, double.PositiveInfinity);
        Assert.True(narrow.Height > wide.Height);
        Assert.True(narrow.Width <= 200);
    }

    [Fact]
    public void InlineStylesBecomeSpans()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("plain **bold** *italic* ***both*** ~~gone~~ ==marked== ++added++ H~2~O x^2^ `code`");
        Assert.Equal(FontAttributes.Bold, SpanWith(view, "bold").FontAttributes);
        Assert.Equal(FontAttributes.Italic, SpanWith(view, "italic").FontAttributes);
        Assert.Equal(FontAttributes.Bold | FontAttributes.Italic, SpanWith(view, "both").FontAttributes);
        Assert.Equal(TextDecorations.Strikethrough, SpanWith(view, "gone").TextDecorations);
        Assert.NotNull(SpanWith(view, "marked").BackgroundColor);
        Assert.Equal(TextDecorations.Underline, SpanWith(view, "added").TextDecorations);
        Assert.Equal(SkUiSpan.SubscriptShift, SpanWith(view, "2").BaselineShift);
        Assert.Equal(SkUiSpan.SuperscriptShift, Nodes<SkUiCoreLabel>(view).SelectMany(label => label.Spans).Last(span => span.Text == "2").BaselineShift);
        Assert.Equal(SkUiTestHelpers.BundledFontFamily, SpanWith(view, "code").FontFamily);
        Assert.NotNull(SpanWith(view, "code").BackgroundColor);
        Draw(view);
    }

    [Fact]
    public void EqualNeighborsShareASpan()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("one\ntwo three");
        var label = Assert.Single(Nodes<SkUiCoreLabel>(view));
        Assert.Equal("one two three", Assert.Single(label.Spans).Text);
    }

    [Fact]
    public void AHardBreakIsALine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Equal("one\ntwo", AllText(View("one  \ntwo")));
    }

    [Theory]
    [InlineData("[text](https://example.com/a)", "text", "https://example.com/a")]
    [InlineData("<https://example.com/b>", "https://example.com/b", "https://example.com/b")]
    [InlineData("<me@example.com>", "me@example.com", "mailto:me@example.com")]
    [InlineData("see https://example.com/c now", "https://example.com/c", "https://example.com/c")]
    [InlineData("[ref][1]\n\n[1]: https://example.com/d", "ref", "https://example.com/d")]
    public void LinksRaiseTheirTarget(string markdown, string text, string target)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View(markdown);
        string? tapped = null;
        view.LinkTapped += (_, args) => tapped = args.Href;
        var span = SpanWith(view, text);
        Assert.Equal(TextDecorations.Underline, span.TextDecorations);
        span.RaiseTapped(new SkUiTappedEventArgs(default));
        Assert.Equal(target, tapped);
    }

    [Fact]
    public void LinkCommandRunsWithTheTarget()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string? target = null;
        var view = View("[a](x:y)").SetLinkTappedCommand(new Command<string>(value => target = value));
        SpanWith(view, "a").RaiseTapped(new SkUiTappedEventArgs(default));
        Assert.Equal("x:y", target);
    }

    [Fact]
    public void EmojiShortcodesBecomeEmojiAndSmileysStayText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var text = AllText(View(":smile: :)"));
        Assert.Contains("😄", text);
        Assert.Contains(":)", text);
    }

    [Fact]
    public void RawHtmlShowsAsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Contains("<b>", AllText(View("a <b>bold</b> b")));
        Assert.Contains("<script>", AllText(View("<script>alert(1)</script>")));
    }

    [Theory]
    [InlineData(1, SemanticHeadingLevel.Level1)]
    [InlineData(3, SemanticHeadingLevel.Level3)]
    [InlineData(6, SemanticHeadingLevel.Level6)]
    public void HeadingsHaveTheirLevelAndSize(int level, SemanticHeadingLevel semantic)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var label = Assert.Single(Nodes<SkUiCoreLabel>(View(new string('#', level) + " Title")));
        Assert.Equal(semantic, label.SemanticHeadingLevel);
        Assert.Equal(FontAttributes.Bold, label.FontAttributes);
    }

    [Fact]
    public void ARuleIsAsWideAsTheDocument()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("a\n\n---\n\nb");
        Draw(view);
        var rule = Assert.Single(Nodes<SkUiCoreBox>(view));
        Assert.Equal(Width, rule.Frame.Width);
        Assert.Equal(1, rule.Frame.Height);
    }

    [Fact]
    public void QuotesHoldBlocksAndTheirBarIsAsTallAsTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("> # Title\n>\n> text\n>\n> > nested");
        Draw(view);
        Assert.Equal("Title\ntext\nnested", AllText(view));
        var bars = Nodes<SkUiCoreBox>(view).ToArray();
        Assert.Equal(2, bars.Length);
        Assert.True(bars[0].Frame.Height > bars[1].Frame.Height);
        Assert.True(bars[1].Frame.Height > 0);
    }

    [Theory]
    [InlineData("NOTE", "Note")]
    [InlineData("TIP", "Tip")]
    [InlineData("IMPORTANT", "Important")]
    [InlineData("WARNING", "Warning")]
    [InlineData("CAUTION", "Caution")]
    public void AlertsTakeTheirTitleFromTheStyle(string kind, string title)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Equal(title + "\nbody", AllText(View($"> [!{kind}]\n> body")));
    }

    [Fact]
    public void AnAlertTitleCanBeLocalized()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { AlertNoteTitle = "Hinweis" }).SetText("> [!NOTE]\n> x");
        Assert.StartsWith("Hinweis", AllText(view));
    }

    [Fact]
    public void ContainersHoldBlocks()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Equal("info\ninside\nmore", AllText(View(":::info\ninside\n\nmore\n:::")));
    }

    [Fact]
    public void ListsNumberFromTheirStartAndNest()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("3. three\n4. four\n   - nested\n\n- bullet");
        Assert.Equal("3.\nthree\n4.\nfour\n◦\nnested\n•\nbullet", AllText(view));
        Draw(view);
    }

    [Fact]
    public void AListItemsWrappedLinesStayBesideTheMarker()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("- " + string.Join(" ", Enumerable.Repeat("word", 80)));
        Draw(view);
        var labels = Nodes<SkUiCoreLabel>(view).ToArray();
        Assert.Equal(2, labels.Length);
        Assert.True(labels[1].Frame.X > labels[0].Frame.X);
        Assert.True(labels[1].Frame.Height > labels[0].Frame.Height * 2);
        Assert.True(labels[1].Frame.Right <= Width + 0.5);
    }

    [Fact]
    public void TaskListsShowTheirStateAndTakeNoInput()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("- [x] done\n- [ ] open");
        var boxes = Nodes<SkUiCoreCheckBox>(view).ToArray();
        Assert.Equal(2, boxes.Length);
        Assert.True(boxes[0].IsChecked);
        Assert.False(boxes[1].IsChecked);
        Assert.True(boxes[0].InputTransparent);
        Assert.Equal("done", boxes[0].SemanticDescription);
        Assert.Equal("done\nopen", AllText(view));
    }

    [Fact]
    public void CodeBlocksKeepTheirLinesAndScroll()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("```csharp\nvar a = 1;\n" + new string('x', 400) + "\n```");
        Draw(view);
        var scroll = Assert.Single(Nodes<SkUiCoreScrollView>(view));
        Assert.Equal(ScrollOrientation.Horizontal, scroll.Orientation);
        Assert.True(scroll.ContentSize.Width > Width);
        Assert.True(scroll.Frame.Width <= Width);
        Assert.Contains("var a = 1;\nxxx", AllText(view));
    }

    [Fact]
    public void IndentedCodeIsACodeBlock()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("    indented");
        Assert.Single(Nodes<SkUiCoreScrollView>(view));
    }

    [Fact]
    public async Task TheCopyButtonCopiesTheCode()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("```\ncode\n```");
        var copied = new TaskCompletionSource<string>();
        view.ClipboardWriter = code =>
        {
            copied.SetResult(code);
            return Task.CompletedTask;
        };
        Tap(view, Assert.Single(Nodes<SkUiCoreButton>(view)));
        Assert.Equal("code", await copied.Task);
    }

    [Fact]
    public void TheCopyButtonCanBeHidden()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { ShowCodeCopyButton = false }).SetText("```\ncode\n```");
        Assert.Empty(Nodes<SkUiCoreButton>(view));
    }

    [Fact]
    public void TheHighlighterColorsCodeAndItsFailureShowsItPlain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string? language = null;
        var colored = View("```cs\nx\n```").SetCodeHighlighter((code, lang) =>
        {
            language = lang;
            return [new SkUiCoreSpan(code).SetTextColor(Colors.Red)];
        });
        Assert.Equal("cs", language);
        Assert.Equal(Colors.Red, Nodes<SkUiCoreLabel>(colored).SelectMany(label => label.Spans).First(span => span.Text == "x").TextColor);

        string? warning = null;
        var failing = new SkUiCoreMarkdown().SetMarkdownStyle(Style);
        failing.Warning += (_, message) => warning = message;
        failing.SetCodeHighlighter((_, _) => throw new InvalidOperationException("boom")).SetText("```\nx\n```");
        Assert.Contains("boom", warning);
        Assert.Contains("x", AllText(failing));
    }

    [Fact]
    public void PipeTablesPlaceCellsAndAlignColumns()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("| a | b | c |\n|:--|:-:|--:|\n| 1 | 2 | 3 |\n| 4 | 5 | 6 |");
        Draw(view);
        var table = Assert.Single(Nodes<SkUiCoreTable>(view));
        Assert.Equal(3, table.RowCount);
        Assert.Equal(3, table.ColumnCount);
        Assert.NotNull(table.GetRowBackground(0));
        Assert.Null(table.GetRowBackground(1));
        var labels = Nodes<SkUiCoreLabel>(view).ToDictionary(AllTextOf);
        Assert.Equal(FontAttributes.Bold, labels["a"].FontAttributes);
        Assert.Equal(TextAlignment.Start, labels["4"].HorizontalTextAlignment);
        Assert.Equal(TextAlignment.Center, labels["5"].HorizontalTextAlignment);
        Assert.Equal(TextAlignment.End, labels["6"].HorizontalTextAlignment);
        Assert.Equal(2, table.GetRow(labels["4"]));
        Assert.Equal(1, table.GetColumn(labels["5"]));
    }

    private static string AllTextOf(SkUiCoreLabel label) => label.Spans.Count > 0 ? string.Concat(label.Spans.Select(span => span.Text)) : label.Text;

    [Fact]
    public void GridTablesSpanAndHoldBlocks()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("""
            +-----+---+
            | a   | b |
            +=====+===+
            | wide    |
            +-----+---+
            | - x | y |
            | - z |   |
            +-----+---+
            """);
        Draw(view);
        var table = Assert.Single(Nodes<SkUiCoreTable>(view));
        var labels = Nodes<SkUiCoreLabel>(view).ToLookup(AllTextOf);
        Assert.Equal(2, table.GetColumnSpan(labels["wide"].Single()));
        Assert.NotEmpty(Nodes<SkUiCoreGrid>(view).Where(grid => grid is not SkUiCoreTable));
        Assert.Single(labels["x"]);
        Assert.Single(labels["z"]);
    }

    [Fact]
    public void AWideTableScrolls()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var header = "|" + string.Concat(Enumerable.Repeat(" column |", 20));
        var rule = "|" + string.Concat(Enumerable.Repeat("---|", 20));
        var view = View(header + "\n" + rule + "\n" + header);
        Draw(view);
        var scroll = Assert.Single(Nodes<SkUiCoreScrollView>(view));
        Assert.True(scroll.ContentSize.Width > Width);
        Assert.True(scroll.Frame.Width <= Width);
    }

    [Fact]
    public void LongTextInACellWraps()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { TableMaximumColumnWidth = 100 })
            .SetText("| a |\n|---|\n| " + string.Join(" ", Enumerable.Repeat("word", 50)) + " |");
        Draw(view);
        var label = Nodes<SkUiCoreLabel>(view).Last();
        Assert.True(label.Frame.Width <= 100.5);
        Assert.True(label.Frame.Height > 50);
    }

    [Fact]
    public void FootnotesGoLastWithSmallMarks()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("text[^n] more\n\n[^n]: the note\n\nafter");
        var text = AllText(view);
        Assert.Contains("[1]", text);
        Assert.EndsWith("1.\nthe note", text);
        Assert.Equal(SkUiSpan.SuperscriptShift, SpanWith(view, "[1]").BaselineShift);
    }

    [Fact]
    public void DefinitionListsIndentTheirDefinitions()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("Term\n:   Meaning");
        Draw(view);
        var labels = Nodes<SkUiCoreLabel>(view).ToDictionary(AllTextOf);
        Assert.Equal(FontAttributes.Bold, labels["Term"].FontAttributes);
        Assert.True(labels["Meaning"].Frame.X > labels["Term"].Frame.X);
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("icons/a.png", true)]
    [InlineData("/etc/passwd", false)]
    [InlineData("../secret.png", false)]
    [InlineData("a/../../secret.png", false)]
    [InlineData("./a.png", false)]
    [InlineData("%2e%2e/secret.png", false)]
    [InlineData("a\\..\\b.png", false)]
    [InlineData("C:\\a.png", false)]
    [InlineData("C:a.png", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("FILE:///etc/passwd", false)]
    [InlineData("//example.com/a.png", false)]
    [InlineData("\\\\host\\a.png", false)]
    [InlineData("~/a.png", false)]
    [InlineData("content://a/b.png", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("http://example.com/a.png", false)]
    [InlineData("https://example.com/a.png", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void WhatADocumentMayLoadByDefault(string url, bool loads)
    {
        var source = SkUiMarkdownImages.Resolve(url);
        Assert.Equal(loads, source is not null);
        if (source is not null)
        {
            var file = Assert.IsType<SkUiFileImageSource>(source);
            Assert.False(file.IsAbsolute);
        }
    }

    [Theory]
    [InlineData("http://example.com/a.png")]
    [InlineData("HTTPS://example.com/a.png")]
    public void RemoteImagesLoadOnlyWhenAllowed(string url) => Assert.IsType<SkUiUriImageSource>(SkUiMarkdownImages.Resolve(url, allowRemote: true));

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("ftp://example.com/a.png")]
    public void AllowingRemoteImagesAllowsNothingElse(string url) => Assert.Null(SkUiMarkdownImages.Resolve(url, allowRemote: true));

    [Theory]
    [InlineData("data:text/html;base64,PGI+")]
    [InlineData("data:image/png,notbase64")]
    [InlineData("data:image/png;base64")]
    [InlineData("data:image/svg+xml;base64,@@@")]
    public void BadDataUrlsDoNotLoad(string url) => Assert.Null(SkUiMarkdownImages.Resolve(url));

    [Fact]
    public void TooLongDataUrlsDoNotLoad() =>
        Assert.Null(SkUiMarkdownImages.Resolve("data:image/png;base64," + new string('A', SkUiMarkdownImages.MaximumDataUrlLength)));

    [Fact]
    public void ABlockedImageShowsItsAltTextAndStillTaps()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("![a cat](https://example.com/cat.png)");
        SkUiMarkdownImageTappedEventArgs? tapped = null;
        view.ImageTapped += (_, args) => tapped = args;
        var block = Assert.Single(Nodes<SkUiMarkdownImageBlock>(view));
        Assert.True(block.ShowsAltText);
        Assert.Equal("a cat", AllText(view));
        Assert.Equal("a cat", block.SemanticDescription);
        Assert.Empty(Nodes<SkUiCoreImage>(view));
        Tap(view, block.Content!);
        Assert.Equal("https://example.com/cat.png", tapped?.Source);
        Assert.Equal("a cat", tapped?.AltText);
    }

    /// <summary>A press and release at the middle of <paramref name="node"/>, through the view's router.</summary>
    private static void Tap(SkUiCoreNode root, SkUiCoreNode node)
    {
        Draw(root);
        var point = new Point(node.Frame.Width / 2, node.Frame.Height / 2);
        for (var parent = node; !ReferenceEquals(parent, root); parent = (SkUiCoreNode)parent.Parent!)
            point = new Point(point.X + parent.Frame.X, point.Y + parent.Frame.Y);
        root.Touch(new(1, SkUiTouchAction.Pressed, point, TimeSpan.Zero));
        root.Touch(new(1, SkUiTouchAction.Released, point, TimeSpan.FromMilliseconds(30)));
    }

    [Fact]
    public void AnImageWithoutAltTextSaysSo()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Equal("Image", AllText(View("![](/etc/passwd)")));
    }

    [Fact]
    public void TheResolverReplacesTheRule()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var urls = new List<string>();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetImageResolver(url =>
        {
            urls.Add(url);
            return null;
        }).SetText("![a](b.png)");
        Assert.Equal(["b.png"], urls);
        Assert.True(Assert.Single(Nodes<SkUiMarkdownImageBlock>(view)).ShowsAltText);
    }

    private const string RedDot = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    [Fact]
    public async Task ADataImageLoadsAndTheDocumentGrows()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"![dot]({RedDot})");
        Draw(view);
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view));
        var before = view.Measure(Width, double.PositiveInfinity).Height;
        await WaitFor(() => picture.ImageSize.Width > 0);
        Assert.Equal(1, picture.ImageSize.Width);
        Assert.True(view.Measure(Width, double.PositiveInfinity).Height > before);
        Assert.False(Assert.Single(Nodes<SkUiMarkdownImageBlock>(view)).ShowsAltText);
    }

    [Fact]
    public async Task ABadDataImageShowsItsAltText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("![broken](data:image/png;base64,AAAA)");
        Draw(view);
        var block = Assert.Single(Nodes<SkUiMarkdownImageBlock>(view));
        await WaitFor(() => block.ShowsAltText);
        Assert.Equal("broken", AllText(view));
    }

    [Fact]
    public void SizeAttributesReserveTheSpaceBeforeALoad()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"![dot]({RedDot}){{width=200 height=100 aspect=Fill horizontal=Center}}");
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view));
        Assert.Equal(200, picture.Width);
        Assert.Equal(100, picture.Height);
        Assert.Equal(Aspect.Fill, picture.Aspect);
        Assert.Equal(LayoutAlignment.Center, picture.HorizontalAlignment);
        Assert.Equal(100, Draw(view).Height);
        Assert.Equal(Width / 2, picture.Frame.Center.X, 1);
    }

    [Theory]
    [InlineData("width=-1")]
    [InlineData("width=NaN")]
    [InlineData("width=Infinity")]
    [InlineData("width=1e308")]
    [InlineData("aspect=99")]
    [InlineData("horizontal=99")]
    public void BadAttributesAreIgnored(string attribute)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"![dot]({RedDot}){{{attribute}}}");
        Assert.True(double.IsNaN(Assert.Single(Nodes<SkUiMarkdownPicture>(view)).Width));
        Assert.Equal(Aspect.AspectFit, Nodes<SkUiMarkdownPicture>(view).Single().Aspect);
        Draw(view);
    }

    [Fact]
    public void ASizedImageStartsAtTheStartAndATapBesideItIsNotOnIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"![dot]({RedDot}){{width=100 height=50}}");
        var taps = 0;
        view.ImageTapped += (_, _) => taps++;
        Draw(view);
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view));
        Assert.Equal(0, picture.Frame.X);
        view.Touch(new(1, SkUiTouchAction.Pressed, new Point(300, 25), TimeSpan.Zero));
        view.Touch(new(1, SkUiTouchAction.Released, new Point(300, 25), TimeSpan.FromMilliseconds(30)));
        Assert.Equal(0, taps);
        view.Touch(new(2, SkUiTouchAction.Pressed, new Point(50, 25), TimeSpan.FromSeconds(1)));
        view.Touch(new(2, SkUiTouchAction.Released, new Point(50, 25), TimeSpan.FromMilliseconds(1030)));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void AListInACellIsAsNarrowAsItsText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("""
            +-----+---+
            | - x | y |
            | - z |   |
            +-----+---+
            """);
        Draw(view);
        var table = Assert.Single(Nodes<SkUiCoreTable>(view));
        Assert.True(table.GetColumnWidth(0) < 100, table.GetColumnWidth(0).ToString());
    }

    [Fact]
    public void ARowOfImagesWraps()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.Single(Nodes<SkUiCoreWrapLayout>(View($"![a]({RedDot}) ![b]({RedDot})\n[![c]({RedDot})](x:y)")));
    }

    [Fact]
    public void ImagesAmongTextCutTheParagraph()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"before ![a]({RedDot}) after");
        var stackChildren = Nodes<SkUiCoreVerticalStackLayout>(view).First().Children;
        Assert.Equal(3, stackChildren.Count);
        Assert.Equal("before \n after", AllText(view));
    }

    [Fact]
    public void ALinkedImageTapsAsTheLink()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View($"[![a]({RedDot}){{width=20 height=20}}](x:y)");
        string? target = null;
        var images = 0;
        view.LinkTapped += (_, args) => target = args.Href;
        view.ImageTapped += (_, _) => images++;
        Tap(view, Assert.Single(Nodes<SkUiMarkdownPicture>(view)));
        Assert.Equal("x:y", target);
        Assert.Equal(0, images);
    }

    [Fact]
    public async Task AnSvgDataImageLoads()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var svg = Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='10'><rect width='20' height='10' fill='currentColor'/></svg>");
        var view = View($"![s](data:image/svg+xml,{svg})");
        Draw(view);
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view));
        await WaitFor(() => picture.ImageSize.Width > 0);
        Assert.True(picture.ImageSize.Width >= 20);
    }

    [Fact]
    public async Task AnSvgInARowLoadsAndTakesItsSize()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var svg = Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='10'><rect width='20' height='10' fill='currentColor'/></svg>");
        var view = View($"![a]({RedDot}){{width=16 height=16}} ![s](data:image/svg+xml,{svg})");
        Draw(view);
        var picture = Nodes<SkUiMarkdownPicture>(view).Last();
        await WaitFor(() => picture.ImageSize.Width > 0 || picture.LoadError is not null);
        Assert.Null(picture.LoadError);
        Draw(view);
        Assert.True(picture.Frame.Width >= 20, picture.Frame.ToString());
        Assert.True(picture.Frame.Height >= 10, picture.Frame.ToString());
    }

    [Fact]
    public async Task AnSvgTheReaderCannotDrawShowsItsAltText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var svg = Uri.EscapeDataString("<svg xmlns='http://www.w3.org/2000/svg' width='20' height='10'><filter id='f'/><use href='#f'/></svg>");
        var view = View($"![not drawn](data:image/svg+xml,{svg})");
        Draw(view);
        var block = Assert.Single(Nodes<SkUiMarkdownImageBlock>(view));
        await WaitFor(() => block.ShowsAltText);
    }

    [Fact]
    public void ImagesPastTheLimitShowTheirAltText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { MaximumImages = 1 }).SetText($"![a]({RedDot})\n\n![b]({RedDot})");
        var blocks = Nodes<SkUiMarkdownImageBlock>(view).ToArray();
        Assert.False(blocks[0].ShowsAltText);
        Assert.True(blocks[1].ShowsAltText);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500 && !condition(); attempt++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    [Fact]
    public void AnAppendKeepsTheBlocksBeforeIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("# Title\n\nfirst\n\nsec");
        var labels = Nodes<SkUiCoreLabel>(view).ToArray();
        view.SetText("# Title\n\nfirst\n\nsecond and more");
        var after = Nodes<SkUiCoreLabel>(view).ToArray();
        Assert.Same(labels[0], after[0]);
        Assert.Same(labels[1], after[1]);
        Assert.NotSame(labels[2], after[2]);
        Assert.Equal("second and more", AllTextOf(after[2]));
    }

    [Fact]
    public void AnEditInTheMiddleKeepsTheBlocksAroundIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("a\n\nb\n\nc");
        var labels = Nodes<SkUiCoreLabel>(view).ToArray();
        view.SetText("a\n\nchanged\n\nnew\n\nc");
        var after = Nodes<SkUiCoreLabel>(view).ToArray();
        Assert.Equal(4, after.Length);
        Assert.Same(labels[0], after[0]);
        Assert.Same(labels[2], after[3]);
        Assert.Equal("a\nchanged\nnew\nc", AllText(view));
    }

    [Fact]
    public void AReferenceDefinedLaterChangesTheBlocksBeforeIt()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("[a][r]\n\nb");
        Assert.Equal("[a][r]\nb", AllText(view));
        view.SetText("[a][r]\n\nb\n\n[r]: x:y");
        string? target = null;
        view.LinkTapped += (_, args) => target = args.Href;
        SpanWith(view, "a").RaiseTapped(new SkUiTappedEventArgs(default));
        Assert.Equal("x:y", target);
    }

    [Fact]
    public void AFootnoteMovedByAnEarlierOneIsRenumbered()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("a\n\nb[^2]\n\n[^1]: one\n[^2]: two");
        Assert.Contains("b[1]", AllText(view));
        view.SetText("a[^1]\n\nb[^2]\n\n[^1]: one\n[^2]: two");
        Assert.Contains("b[2]", AllText(view));
    }

    [Fact]
    public void ANewStyleBuildsEverythingAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("a");
        var label = Assert.Single(Nodes<SkUiCoreLabel>(view));
        view.SetMarkdownStyle(Style with { TextColor = Colors.Red });
        var after = Assert.Single(Nodes<SkUiCoreLabel>(view));
        Assert.NotSame(label, after);
        Assert.Equal(Colors.Red, after.TextColor);
    }

    [Fact]
    public async Task ByDefaultNothingOfADocumentIsBuiltOnTheCallersThread()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        using var returned = new ManualResetEventSlim();
        var builtAfterSetTextReturned = false;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background).SetCodeHighlighter((_, _) =>
        {
            // In place, this would wait for itself.
            builtAfterSetTextReturned = returned.Wait(TimeSpan.FromSeconds(5));
            return null;
        });
        view.SetText("```\ncode\n```");
        returned.Set();
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(builtAfterSetTextReturned);
        Assert.NotEmpty(Nodes<SkUiCoreLabel>(view));
    }

    [Fact]
    public async Task WhatABuildMadeGoesOnScreenThroughTheDispatcherOfTheThreadThatSetTheText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var ui = new QueueDispatcher();
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(ui);
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background);
        var warnings = 0;
        view.Warning += (_, _) => warnings++;
        view.SetCodeHighlighter((_, _) => throw new InvalidOperationException("boom"));
        view.SetText($"```\ncode\n```\n\n- [x] done\n\n![dot]({RedDot})");
        await ui.WaitForWorkAsync();
        // Built, not applied: the tree is as it was, the warning waits, and neither the task's state nor the image started.
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
        Assert.Equal(0, warnings);
        ui.RunAll();
        Assert.Equal(1, warnings);
        Assert.True(Assert.Single(Nodes<SkUiCoreCheckBox>(view)).IsChecked);
        Assert.Single(Nodes<SkUiMarkdownPicture>(view));
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task AClosedDispatcherEndsTheWaitAndTheNextTextStillBuilds()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var ui = new QueueDispatcher { IsClosed = true };
        using var dispatcher = SkUiTestHelpers.UseTestDispatcher(ui);
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background);
        view.SetText("first");
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
        ui.IsClosed = false;
        view.SetText("second");
        await ui.WaitForWorkAsync();
        ui.RunAll();
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Single(Nodes<SkUiCoreLabel>(view));
    }

    internal sealed class QueueDispatcher : IDispatcher
    {
        private readonly Queue<Action> _work = new();

        public bool IsDispatchRequired => true;

        internal bool IsClosed { get; set; }

        public bool Dispatch(Action action)
        {
            if (IsClosed)
                return false;
            lock (_work)
                _work.Enqueue(action);
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) => Dispatch(action);

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();

        internal async Task WaitForWorkAsync()
        {
            for (var attempt = 0; attempt < 3000; attempt++)
            {
                lock (_work)
                    if (_work.Count > 0)
                        return;
                await Task.Delay(10);
            }
            Assert.Fail("Nothing was dispatched.");
        }

        internal void RunAll()
        {
            while (true)
            {
                Action action;
                lock (_work)
                {
                    if (_work.Count == 0)
                        return;
                    action = _work.Dequeue();
                }
                action();
            }
        }
    }

    [Fact]
    public async Task TheLatestTextWinsAndBuildsDoNotPileUp()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var builds = 0;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background).SetCodeHighlighter((_, _) =>
        {
            Interlocked.Increment(ref builds);
            Thread.Sleep(20);
            return null;
        });
        for (var index = 0; index < 50; index++)
            view.SetText("```\ncode " + index + "\n```");
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Contains("code 49", AllText(view));
        Assert.True(builds <= 3, builds.ToString());
    }

    [Fact]
    public async Task AnEmptyTextAfterALargeOneWins()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background);
        view.SetText(string.Join("\n\n", Enumerable.Repeat("one two three", 4000)));
        view.SetText("");
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        await Task.Delay(300);
        Assert.Empty(Nodes<SkUiCoreLabel>(view));
    }

    [Fact]
    public async Task AppendsInTheBackgroundKeepTheBlocksBeforeThem()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Background).SetText("# Title\n\nsec");
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        var title = Nodes<SkUiCoreLabel>(view).First();
        view.SetText("# Title\n\nsecond");
        await view.WhenUpdated.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Same(title, Nodes<SkUiCoreLabel>(view).First());
        Assert.Equal("Title\nsecond", AllText(view));
    }

    public static TheoryData<string, string> HostileDocuments() => new()
    {
        { "deep quotes", string.Concat(Enumerable.Repeat("> ", 200)) + "x" },
        { "deep lists", string.Concat(Enumerable.Range(0, 120).Select(level => new string(' ', level * 2) + "- x\n")) },
        { "deep emphasis", string.Concat(Enumerable.Repeat("*a **b ", 4000)) },
        { "unclosed links", string.Concat(Enumerable.Repeat("[a](", 40000)) },
        { "many delimiters", string.Concat(Enumerable.Repeat("*a ", 50000)) },
        { "deep links", string.Concat(Enumerable.Repeat("[", 3000)) + "x" + string.Concat(Enumerable.Repeat("](u)", 3000)) },
    };

    [Theory]
    [MemberData(nameof(HostileDocuments))]
    public async Task HostileDocumentsNeitherThrowNorEndTheProcess(string name, string text)
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        Assert.NotNull(name);
        Exception? failure = null;
        SkUiCoreMarkdown? view = null;
        // A secondary thread's stack on Apple platforms.
        var thread = new Thread(() =>
        {
            try
            {
                view = new SkUiCoreMarkdown().SetMarkdownStyle(Background).SetText(text);
                view.WhenUpdated.Wait(TimeSpan.FromSeconds(120));
                Draw(view);
                // The parse and the build too, on this small stack.
                view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetText(text);
                Draw(view);
            }
            catch (Exception error)
            {
                failure = error;
            }
        }, 512 * 1024);
        thread.Start();
        await Task.Run(thread.Join);
        Assert.Null(failure);
        Assert.NotNull(view);
    }

    [Fact]
    public void InlinesPastTheBuildersCapShowAsPlainText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string? warning = null;
        var deep = string.Concat(Enumerable.Repeat("*a _b ", 100)) + "x" + string.Concat(Enumerable.Repeat(" b_ a*", 100));
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style);
        view.Warning += (_, message) => warning = message;
        view.SetText(deep);
        Assert.True(view.IsPlainText);
        Assert.NotNull(warning);
        Assert.Equal(deep, AllText(view));
        Draw(view);
    }

    [Fact]
    public void ADocumentOverTheLimitIsCutAndPlain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        string? warning = null;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { MaximumInputLength = 10 });
        view.Warning += (_, message) => warning = message;
        view.SetText("# " + new string('a', 100));
        Assert.True(view.IsPlainText);
        Assert.NotNull(warning);
        Assert.Equal(10, AllText(view).Length);
    }

    [Fact]
    public void ListItemsWithNothingInThemCountAsBlocks()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var empty = string.Join("\n", Enumerable.Repeat("-", 10));
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { MaximumBlocks = 5 }).SetText(empty);
        Assert.True(view.IsPlainText);
        view.SetMarkdownStyle(Style with { MaximumBlocks = 11 });
        Assert.False(view.IsPlainText);
    }

    [Fact]
    public void TooManyBlocksShowAsPlainText()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { MaximumBlocks = 3 }).SetText("a\n\nb\n\nc\n\nd\n\ne");
        Assert.True(view.IsPlainText);
        Assert.Equal("a\n\nb\n\nc\n\nd\n\ne", AllText(view));
        // And back, when the text fits again.
        view.SetText("a");
        Assert.False(view.IsPlainText);
        Assert.Equal("a", AllText(view));
    }

    [Fact]
    public void ATableOverTheLimitShowsItsSource()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style with { MaximumTableCells = 2 }).SetText("| a | b |\n|---|---|\n| 1 | 2 |");
        Assert.Empty(Nodes<SkUiCoreTable>(view));
        Assert.Contains("| 1 | 2 |", AllText(view));
    }

    [Fact]
    public void ALinkDoesNotOpenAndNothingLoadsByItself()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = View("[a](https://example.com) ![b](https://example.com/b.png) ![c](file:///etc/passwd)");
        Assert.Empty(Nodes<SkUiCoreImage>(view));
    }

    [Fact]
    public void TheSkUiViewHostsTheSameEngine()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiMarkdownView { MarkdownStyle = Style, Text = "**a**" };
        Assert.Same(view.Markdown, view.Content);
        Assert.Equal(FontAttributes.Bold, SpanWith(view.Markdown, "a").FontAttributes);
        var measured = view.Measure(Width, double.PositiveInfinity);
        Assert.True(measured.Height > 0);
        view.Text = "";
        Assert.Empty(Nodes<SkUiCoreLabel>(view.Markdown));
    }

    [Theory]
    [InlineData("# A", "intro\n\n# A")]
    [InlineData("intro\n\n# A", "# A")]
    public void AHeadingThatStopsOrStartsBeingFirstIsBuiltAgain(string before, string after)
    {
        var view = View(before);
        view.SetText(after);
        var heading = Nodes<SkUiCoreLabel>(view).Last();
        var isFirst = !after.StartsWith("intro", StringComparison.Ordinal);
        Assert.Equal(isFirst ? 0 : Style.HeadingTopSpacing, heading.Margin.Top);
    }

    [Fact]
    public void AnImageInARowTakesItsVerticalAttribute()
    {
        var view = View("![a](x.png){vertical=Center} ![b](y.png)");
        var blocks = Nodes<SkUiMarkdownImageBlock>(view).ToArray();
        Assert.Equal(2, blocks.Length);
        Assert.Equal(LayoutAlignment.Center, blocks[0].VerticalAlignment);
        Assert.Equal(LayoutAlignment.Start, blocks[1].VerticalAlignment);
    }

    [Fact]
    public void BindingsSeeThePlainTextChange()
    {
        var view = new SkUiMarkdownView().SetMarkdownStyle(Style with { MaximumBlocks = 1 });
        var changes = new List<string?>();
        view.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        view.SetText("a\n\nb\n\nc");
        Assert.True(view.IsPlainText);
        Assert.Contains(nameof(SkUiMarkdownView.IsPlainText), changes);
        view.SetText("a");
        Assert.False(view.IsPlainText);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference SetOldText(SkUiCoreMarkdown view)
    {
        var text = "![a](missing.png)\n\n" + new string('x', 1000) + view.GetHashCode();
        view.SetText(text);
        return new WeakReference(text);
    }

    /// <summary>An image block outlives its build, and streaming keeps the first blocks of a document: they must not keep each version of its text.</summary>
    [Fact]
    public void WhatStaysOnScreenDoesNotKeepOldText()
    {
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style);
        var old = SetOldText(view);
        view.SetText("![a](missing.png)\n\nnew");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(old.IsAlive);
        Assert.Single(Nodes<SkUiMarkdownImageBlock>(view));
    }

    [Theory]
    [InlineData(nameof(SkUiMarkdownStyle.MaximumInputLength))]
    [InlineData(nameof(SkUiMarkdownStyle.BlockSpacing))]
    [InlineData(nameof(SkUiMarkdownStyle.RuleThickness))]
    [InlineData(nameof(SkUiMarkdownStyle.FontSize))]
    [InlineData(nameof(SkUiMarkdownStyle.CodePadding))]
    public void AStyleOutOfRangeIsRefused(string name)
    {
        var style = name switch
        {
            nameof(SkUiMarkdownStyle.MaximumInputLength) => Style with { MaximumInputLength = -1 },
            nameof(SkUiMarkdownStyle.BlockSpacing) => Style with { BlockSpacing = double.NaN },
            nameof(SkUiMarkdownStyle.RuleThickness) => Style with { RuleThickness = -1 },
            nameof(SkUiMarkdownStyle.FontSize) => Style with { FontSize = 0 },
            _ => Style with { CodePadding = new Thickness(-1) }
        };
        var core = new SkUiCoreMarkdown().SetMarkdownStyle(Style);
        var error = Assert.Throws<ArgumentException>(() => core.SetMarkdownStyle(style));
        Assert.Contains(name, error.Message);
        Assert.Same(Style, core.MarkdownStyle);
        var view = new SkUiMarkdownView().SetMarkdownStyle(Style);
        Assert.Throws<ArgumentException>(() => view.SetMarkdownStyle(style));
        view.MarkdownStyle = style; // a bindable property ignores what it does not take, with a warning, as MAUI does
        Assert.Same(Style, view.MarkdownStyle);
    }
}

/// <summary>What is left out of the count of delimiters, and what must not be: a document that gets past it can hold a parse for minutes.</summary>
public class MarkdownDelimiterCountTests
{
    private static readonly SkUiMarkdownStyle Style = SkUiMarkdownStyle.Default with
    {
        FontFamily = SkUiTestHelpers.BundledFontFamily,
        CodeFontFamily = SkUiTestHelpers.BundledFontFamily,
        MaximumInPlaceParseLength = int.MaxValue
    };

    private static string R(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    public static TheoryData<string> Ordinary() => new()
    {
        // 220 KB of an image's data, thousands of + in it.
        "![pic](data:image/png;base64," + R("ab+/", 55_000) + "==)",
        // A long tight list: each item is a run of its own.
        R("- [item](https://example.com/a): text\n", 1_100),
        R("* [item](https://example.com/a): text\n", 1_100),
        "1. a\n" + R("2. [item](https://example.com/a): text\n", 1_100),
        R("# [a](x:y) *b*\n", 1_500),
        // Code is not parsed for delimiters.
        "```json\n" + R("  \"key\": \"[a]*b*: c\",\n", 1_400) + "```\n",
        "~~~\n" + R("[a](*b*\n", 2_000) + "~~~\n",
        "````\n```\n" + R("[a](*b*\n", 2_000),
        "\n    " + string.Join("\n    ", Enumerable.Repeat("[a](*b*", 2_000)),
        // Definitions in numbers a document has.
        R("[a]: https://example.com\n", 1_000),
        // A code block or a formula in what holds it is as much code.
        "- a\n\n  ```\n" + R("  [a](*b*\n", 2_000) + "  ```\n",
        "> ```\n" + R("> [a](*b*\n", 2_000) + "> ```\n",
        "::: x\n```\n" + R("[a](*b*\n", 2_000) + "```\n:::\n",
        "$$\n" + R("x^* _a [b]\n", 2_000) + "$$\n",
    };

    [Theory]
    [MemberData(nameof(Ordinary))]
    public void OrdinaryDocumentsAreNotCounted(string text)
    {
        Assert.False(SkUiMarkdownParser.HasPathologicalBlock(text));
        Assert.False(new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetText(text).IsPlainText);
    }

    public static TheoryData<string> Hostile() => new()
    {
        R("[a](", 4_001),
        // One paragraph, whatever its lines look like: these do not end it.
        "x\n" + R("2. [a](\n", 2_001),
        R("#[a](\n", 2_001),
        R("[a](\n    [a](\n", 1_001),
        // In a list, an indented line is a paragraph.
        "- x\n\n" + R("    [a](\n", 2_001),
        // Not fences: the info string of a backtick fence has no backticks, and four spaces make no fence.
        "``` `\n" + R("[a](\n", 2_001),
        "```\n```\n" + R("[a](\n", 2_001),
        // Not an image's data: unfinished, or with what data has not.
        "![a](data:x;base64," + R("++a ", 3_000),
        "![a](data:x;base64," + R("[a](", 2_001) + ")",
        "x](data:;base64,++)" + R("[a](", 4_001),
        R("![a](data:(,+a=b)", 2_000),
        // The alt text of an image is parsed, whatever its destination.
        R("![*a*a](data:,AA)", 2_000),
        // Fences that end with what holds them.
        "- a\n  ```\n" + R("[a](", 4_001),
        "::: x\n```\n:::\n" + R("[a](", 4_001),
        // Footnotes, linked or not: a full parse reads them all.
        "[^1]: " + R("[a](", 4_001),
        "[^1]\n\n[^1]: " + R("[a](", 4_001),
        "x[^1][^2]\n\n[^1]: a\n\n[^2]: " + R("[a](", 4_001),
        "[^1]: > " + R("[a](", 4_001),
        // What takes the block pass itself long: nothing of these is a delimiter in a paragraph.
        R("[a\nb]: x\n", 4_001),
        R("- [a]: x\n", 4_001),
        R("- [^a]: x\n", 4_001),
        "::: x\n" + R("[a\nb]: x\n", 4_001),
        R("[a]: x\n", 4_001),
        R("[a]: x\n\n", 4_001),
        R("> [a]: x\n", 4_001),
        R("[^a]: x\n", 4_001),
        R("a\n=\n", 4_001),
        R("a\n---\n", 4_001),
        // A formula or a definition holding a fence ends it too.
        "$$\n```\n$$\n" + R("[a](", 4_001),
        " $$\n~~~\n $$\n" + R("[a](", 4_001),
        "$$\r\n~~~\r\n$$\r\n" + R("[a](", 4_001),
        "a\n: b\n```\n: c\n" + R("[a](", 4_001),
        // Each block is counted, also inside what holds it, and a grid table as a whole.
        "> " + R("[a](", 4_001),
        "- x\n  - y\n\n    " + R("[a](", 4_001),
        "+---+\n| " + R("*a ", 4_001) + " |\n+---+\n",
        // A line ends at \r too, and what only looks blank does not end a paragraph.
        "~~~\r~~~\r" + R("[a](", 4_001),
        R(R("[a](", 1_000) + "\n\u00A0\n", 3),
        R(R("[a](", 1_000) + "\n\f\n", 3),
    };

    [Theory]
    [MemberData(nameof(Hostile))]
    public void HostileDocumentsAreCounted(string text) => Assert.True(SkUiMarkdownParser.HasPathologicalBlock(text));

    [Fact]
    public void AFootnoteInBoundsIsNotCounted() =>
        Assert.False(SkUiMarkdownParser.HasPathologicalBlock("[^1]: " + R("[a](", 100) + "\n\n" + R("[a](x:y) *b*\n\n", 2_000)));

    [Theory]
    [InlineData(">")]
    [InlineData("- ")]
    public void NestingPastTheParsersLimitsShowsAsPlainText(string level)
    {
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(Style).SetText(R(level, 5_000) + "a");
        Assert.True(view.IsPlainText);
    }
}

/// <summary>What the color scheme does to Markdown views. Replaces the app-wide scheme, which rebuilds every live view, so the class runs alone.</summary>
[Collection(GlobalStateCollection.Name)]
public class MarkdownColorSchemeTests
{
    [Fact]
    public void ANewTextSizeBuildsTheDocumentAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(SkUiMarkdownStyle.Default with { MaximumInPlaceParseLength = int.MaxValue, FontFamily = SkUiTestHelpers.BundledFontFamily })
            .SetMathRenderer(Markdown.Math.SkUiMarkdownMath.Renderer).SetText("see $x^2$ there");
        double Width() => ((SkUiCoreLabel)((SkUiCoreVerticalStackLayout)view.Content!).Children[0]).Spans.Single(span => span.InlineObject is not null).InlineObject!.Width;
        var before = Width();
        try
        {
            SkUiFontScaling.Factor = 2;
            Assert.Equal(before * 2, Width(), 0);
        }
        finally
        {
            SkUiFontScaling.Factor = null;
        }
        Assert.Equal(before, Width(), 1);
    }

    [Fact]
    public void ANewLookBuildsTheDocumentAgain()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(SkUiMarkdownStyle.Default with { MaximumInPlaceParseLength = int.MaxValue, FontFamily = SkUiTestHelpers.BundledFontFamily }).SetText("# a");
        double Size() => ((SkUiCoreLabel)((SkUiCoreVerticalStackLayout)view.Content!).Children[0]).FontSize;
        var before = Size();
        try
        {
            SkUiLook.Current = new LargeLook();
            Assert.Equal(before * 40 / look.DefaultFontSize, Size(), 1);
        }
        finally
        {
            SkUiLook.Current = look;
        }
        Assert.Equal(before, Size(), 1);
    }

    private sealed class LargeLook : DefaultSkUiLook
    {
        public override double DefaultFontSize => 40;
    }

    private sealed class PaddedLook : DefaultSkUiLook
    {
        public override bool TextIncludesFontPadding => true;
    }

    /// <summary>Android's font padding comes from the fonts drawn, and an inline object draws none.</summary>
    [Fact]
    public void FontPaddingCountsNoFontForAnInlineObject()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var look = SkUiLook.Current;
        try
        {
            SkUiLook.Current = new PaddedLook();
            var label = new SkUiCoreLabel().SetSpans(new SkUiCoreSpan("x").SetInlineObject(new SkUiInlineObject(10, 4, 4, _ => { })));
            Assert.Equal(4, label.Measure(100, 100).Height, 1);
        }
        finally
        {
            SkUiLook.Current = look;
        }
    }

    [Fact]
    public void TheAppWideRendererDrawsFormulas()
    {
        var saved = SkUiCoreMarkdown.DefaultMathRenderer;
        try
        {
            SkUiCoreMarkdown.DefaultMathRenderer = null;
            Markdown.Math.SkUiMarkdownMath.UseSkiaUiMarkdownMath(MauiApp.CreateBuilder());
            Assert.Same(Markdown.Math.SkUiMarkdownMath.Renderer, SkUiCoreMarkdown.DefaultMathRenderer);
            var view = new SkUiCoreMarkdown().SetMarkdownStyle(SkUiMarkdownStyle.Default with { MaximumInPlaceParseLength = int.MaxValue }).SetText("$$\nx\n$$");
            Assert.Empty(((IVisualTreeElement)view).GetVisualChildren().OfType<SkUiCoreLabel>());
            Assert.Null(new SkUiCoreMarkdown().MathRenderer); // the view's own stays unset
        }
        finally
        {
            SkUiCoreMarkdown.DefaultMathRenderer = saved;
        }
    }

    [Fact]
    public void ANewSchemeColorsTheDocument()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var scheme = SkUiColorScheme.Current;
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(SkUiMarkdownStyle.Default with { MaximumInPlaceParseLength = int.MaxValue, FontFamily = SkUiTestHelpers.BundledFontFamily }).SetText("a");
        try
        {
            SkUiColorScheme.Current = scheme is DarkSkUiColorScheme ? LightSkUiColorScheme.Instance : DarkSkUiColorScheme.Instance;
            var content = (SkUiCoreVerticalStackLayout)view.Content!;
            Assert.Equal(SkUiColorScheme.Current.DefaultForeground, ((SkUiCoreLabel)content.Children[0]).TextColor);
        }
        finally
        {
            SkUiColorScheme.Current = scheme;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateView(SkUiMarkdownStyle style)
    {
        var view = new SkUiCoreMarkdown().SetMarkdownStyle(style).SetText("[a](x:y) **b**");
        view.Measure(300, double.PositiveInfinity);
        return new WeakReference(view);
    }

    [Fact]
    public void TheSchemeDoesNotKeepAViewAlive()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var scheme = SkUiColorScheme.Current;
        var reference = CreateView(SkUiMarkdownStyle.Default with { MaximumInPlaceParseLength = int.MaxValue, FontFamily = SkUiTestHelpers.BundledFontFamily });
        try
        {
            SkUiColorScheme.Current = scheme is DarkSkUiColorScheme ? LightSkUiColorScheme.Instance : DarkSkUiColorScheme.Instance;
        }
        finally
        {
            SkUiColorScheme.Current = scheme;
        }
        for (var attempt = 0; attempt < 10 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(reference.IsAlive);
    }
}
