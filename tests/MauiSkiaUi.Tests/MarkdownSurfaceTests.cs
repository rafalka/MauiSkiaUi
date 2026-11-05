using MauiSkiaUi.Core;
using MauiSkiaUi.Markdown;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The Markdown view on a drawn surface: what an update records, and how it behaves in a scroll view.</summary>
public class MarkdownSurfaceTests
{
    private const string RedDot = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    private static SkUiMarkdownStyle Style { get; } = SkUiMarkdownStyle.Default with
    {
        FontFamily = SkUiTestHelpers.BundledFontFamily,
        CodeFontFamily = SkUiTestHelpers.BundledFontFamily,
        MaximumInPlaceParseLength = int.MaxValue
    };

    private static IEnumerable<T> Nodes<T>(ISkUiCoreNode root) where T : class
    {
        if (root is T match)
            yield return match;
        foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<ISkUiCoreNode>())
            foreach (var found in Nodes<T>(child))
                yield return found;
    }

    /// <summary>A formula that counts how often it is recorded: a block that keeps its node keeps its picture.</summary>
    private sealed class CountingRenderer : ISkUiMarkdownMathRenderer
    {
        internal int Recordings { get; private set; }

        public SkUiCoreNode CreateBlock(string latex, SkUiMarkdownMathContext context) => new CountingNode(this);

        public SkUiInlineObject? CreateInline(string latex, SkUiMarkdownMathContext context) => null;

        private sealed class CountingNode(CountingRenderer owner) : SkUiCoreNode
        {
            protected override Size MeasureContent(double widthConstraint, double heightConstraint) => new(50, 20);

            protected override void OnPaintContent(SKCanvas canvas) => owner.Recordings++;
        }
    }

    private static (SkUiFrameRenderer Renderer, Queue<Action> Frames) Show(SkUiView root, double width, double height)
    {
        SkUiTestHelpers.Arrange(root, width, height);
        var frames = new Queue<Action>();
        var renderer = new SkUiFrameRenderer(root, frames.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        Frame(frames);
        return (renderer, frames);
    }

    private static void Frame(Queue<Action> frames)
    {
        while (frames.TryDequeue(out var frame))
            frame();
    }

    [Fact]
    public void AnAppendRecordsOnlyTheTail()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var math = new CountingRenderer();
        var view = new SkUiMarkdownView { MarkdownStyle = Style, MathRenderer = math, Text = "$$\nx\n$$\n\nThe answer so" };
        var (renderer, frames) = Show(view, 300, 400);
        Assert.Equal(1, math.Recordings);
        foreach (var more in new[] { " far", " far is", " far is long" })
        {
            view.Text = "$$\nx\n$$\n\nThe answer so" + more;
            SkUiTestHelpers.Arrange(view, 300, 400);
            renderer.RequestFrame();
            Frame(frames);
        }
        Assert.Equal(1, math.Recordings);
        Assert.Contains(Nodes<SkUiCoreLabel>(view.Markdown), label => label.Spans.Any(span => span.Text.EndsWith("far is long")));
        // A new look is another matter.
        view.MarkdownStyle = Style with { BlockSpacing = 20 };
        SkUiTestHelpers.Arrange(view, 300, 400);
        renderer.RequestFrame();
        Frame(frames);
        Assert.Equal(2, math.Recordings);
        renderer.Dispose();
    }

    private static SkUiScrollView InScroll(SkUiMarkdownView view)
    {
        var scroll = new SkUiScrollView { Content = view };
        SkUiTestHelpers.Arrange(scroll, 300, 300);
        return scroll;
    }

    private static void Drag(SkUiView root, Point from, double dx, double dy)
    {
        var time = TimeSpan.Zero;
        root.Touch(new(1, SkUiTouchAction.Pressed, from, time));
        Point at = from;
        for (var step = 1; step <= 8; step++)
        {
            time += TimeSpan.FromMilliseconds(16);
            at = new Point(from.X + dx * step / 8, from.Y + dy * step / 8);
            root.Touch(new(1, SkUiTouchAction.Moved, at, time));
        }
        time += TimeSpan.FromMilliseconds(400); // no fling
        root.Touch(new(1, SkUiTouchAction.Moved, at, time));
        root.Touch(new(1, SkUiTouchAction.Released, at, time));
    }

    [Fact]
    public void ADragSidewaysScrollsTheCodeAndADragDownScrollsThePage()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var filler = string.Join("\n\n", Enumerable.Repeat("A paragraph of text.", 40));
        var view = new SkUiMarkdownView { MarkdownStyle = Style with { ShowCodeCopyButton = false }, Text = "```\n" + new string('x', 400) + "\n```\n\n" + filler };
        var page = InScroll(view);
        var code = Assert.Single(Nodes<SkUiCoreScrollView>(view.Markdown));
        SkUiTestHelpers.Arrange(page, 300, 300);
        Drag(page, new Point(150, 20), -80, 0);
        Assert.True(code.ScrollX > 20, code.ScrollX.ToString());
        Assert.Equal(0, page.ScrollY);
        var before = code.ScrollX;
        Drag(page, new Point(150, 20), 0, -80);
        Assert.True(page.ScrollY > 20, page.ScrollY.ToString());
        Assert.Equal(before, code.ScrollX);
    }

    [Fact]
    public async Task AnImageLoadingBelowKeepsTheScrollOffset()
    {
        using var _ = SkUiTestHelpers.UseBundledFont();
        var filler = string.Join("\n\n", Enumerable.Repeat("A paragraph of text.", 40));
        var view = new SkUiMarkdownView { MarkdownStyle = Style, Text = filler + "\n\n![dot](" + RedDot + ")\n\nthe end" };
        var page = InScroll(view);
        await page.ScrollToAsync(0, 100, animated: false);
        var before = page.ScrollY;
        Assert.True(before > 0);
        var height = view.Markdown.DesiredSize.Height;
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view.Markdown));
        for (var attempt = 0; attempt < 300 && picture.ImageSize.Width <= 0; attempt++)
            await Task.Delay(10);
        Assert.True(picture.ImageSize.Width > 0);
        SkUiTestHelpers.Arrange(page, 300, 300);
        Assert.True(view.Markdown.DesiredSize.Height > height);
        Assert.Equal(before, page.ScrollY);
    }

    [Fact]
    public async Task AContentPopupGoesThroughThePresenterAndCloses()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        var content = new SkUiCoreHost();
        SkUiPopupRequest? opened = null;
        var showing = SkUiPopups.ShowContentAsync(anchor, content, new Size(200, 100), presenter, request => opened = request);
        Assert.NotNull(opened);
        Assert.Same(opened, presenter.Current);
        Assert.Same(content, opened.Content);
        Assert.Equal(SkUiPopupPlacement.Window, opened.Placement);
        Assert.False(showing.IsCompleted);
        opened.Cancel();
        Assert.True(await showing);
        Assert.True(opened.IsCompleted);
    }

    [Fact]
    public async Task AContentPopupWithoutAPresenterShowsNothing()
    {
        var saved = SkUiPopups.Presenter;
        SkUiPopups.Presenter = null;
        try
        {
            var anchor = new SkUiLabel();
            Assert.False(await SkUiPopups.ShowContentAsync(anchor, new SkUiCoreHost(), new Size(10, 10)));
        }
        finally
        {
            SkUiPopups.Presenter = saved;
        }
    }

    [Fact]
    public async Task ThePicturePopupShowsAZoomViewAndCloses()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        var showing = SkUiCoreMarkdown.ShowZoomAsync(anchor, SkUiImageSource.FromFile("a.png"), "alt", new Size(300, 300), presenter);
        var request = presenter.Current!;
        var host = Assert.IsType<SkUiCoreHost>(request.Content);
        var zoom = Assert.IsType<SkUiCoreZoomView>(host.Content);
        Assert.Equal("alt", zoom.Content!.SemanticDescription);
        request.Cancel();
        await showing;
        Assert.True(request.IsCompleted);
    }
}
