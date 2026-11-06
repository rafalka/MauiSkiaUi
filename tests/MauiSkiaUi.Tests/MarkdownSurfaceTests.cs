using MauiSkiaUi.Core;
using MauiSkiaUi.Markdown;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>The Markdown view on a drawn surface: what an update records, and how it behaves in a scroll view.</summary>
public class MarkdownSurfaceTests
{
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
        // The image waits until the height without it is known.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = SkUiImageSource.FromStream(async _ =>
        {
            await release.Task;
            return new MemoryStream(ImageLoadingTests.Png(SkiaSharp.SKColors.Red));
        });
        var view = new SkUiMarkdownView { MarkdownStyle = Style, ImageResolver = _ => source, Text = filler + "\n\n![dot](dot.png)\n\nthe end" };
        var page = InScroll(view);
        await page.ScrollToAsync(0, 100, animated: false);
        var before = page.ScrollY;
        Assert.True(before > 0);
        var height = view.Markdown.DesiredSize.Height;
        var picture = Assert.Single(Nodes<SkUiMarkdownPicture>(view.Markdown));
        Assert.True(picture.ImageSize.Width <= 0);
        var loaded = new TaskCompletionSource<SkUiImageLoadStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        picture.LoadingFinished += (_, args) => loaded.TrySetResult(args.Status);
        release.SetResult();
        Assert.Equal(SkUiImageLoadStatus.Succeeded, await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30)));
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

    [Fact]
    public async Task ATapClosesThePicturePopup()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        var showing = SkUiCoreMarkdown.ShowZoomAsync(anchor, SkUiImageSource.FromFile("a.png"), "alt", new Size(300, 300), presenter);
        var zoom = (SkUiCoreZoomView)((SkUiCoreHost)presenter.Current!.Content!).Content!;
        zoom.Measure(300, 300);
        zoom.Arrange(new Rect(0, 0, 300, 300));
        zoom.Touch(new(1, SkUiTouchAction.Pressed, new Point(100, 100), TimeSpan.Zero));
        zoom.Touch(new(1, SkUiTouchAction.Released, new Point(100, 100), TimeSpan.FromMilliseconds(20)));
        // A single tap waits to see that it is no double tap.
        Assert.Same(showing, await Task.WhenAny(showing, Task.Delay(TimeSpan.FromSeconds(10))));
        Assert.True(presenter.Current.IsCompleted);
    }

    [Fact]
    public async Task ACancelledTokenShowsNothing()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        Assert.False(await SkUiPopups.ShowContentAsync(anchor, new SkUiCoreHost(), new Size(100, 100), presenter, null, new CancellationToken(canceled: true)));
        Assert.Empty(presenter.Requests);
    }

    [Fact]
    public async Task AFailingCallbackShowsNothingAndCompletesTheRequest()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        SkUiPopupRequest? request = null;
        Assert.False(await SkUiPopups.ShowContentAsync(anchor, new SkUiCoreHost(), new Size(100, 100), presenter, opened =>
        {
            request = opened;
            throw new InvalidOperationException("boom");
        }));
        Assert.Empty(presenter.Requests);
        Assert.True(request!.IsCompleted);
    }

    /// <summary>A token can be cancelled on any thread; a presenter closes on the thread that showed it.</summary>
    [Fact]
    public async Task CancellingClosesOnTheThreadThatShows()
    {
        var dispatcher = new MarkdownTests.QueueDispatcher();
        using var scope = SkUiTestHelpers.UseTestDispatcher(dispatcher);
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        using var source = new CancellationTokenSource();
        var showing = SkUiPopups.ShowContentAsync(anchor, new SkUiCoreHost(), new Size(100, 100), presenter, null, source.Token);
        await Task.Run(source.Cancel);
        Assert.False(presenter.Current!.CloseRequested.IsCancellationRequested);
        dispatcher.RunAll();
        Assert.True(presenter.Current.CloseRequested.IsCancellationRequested);
        Assert.True(await showing);
    }

    [Fact]
    public async Task TheContentFitsTheRoomThePresenterReports()
    {
        var anchor = new SkUiLabel { Text = "anchor" };
        SkUiTestHelpers.Arrange(anchor, 100, 40);
        var presenter = new RecordingPopupPresenter();
        var content = new SkUiCoreHost();
        var showing = SkUiPopups.ShowContentAsync(anchor, content, new Size(300, 300), presenter);
        var request = presenter.Current!;
        var moves = 0;
        request.BoundsChanged += (_, _) => moves++;
        var room = new Rect(0, 0, 400, request.Bounds.Height / 2);
        request.SetAvailableArea(room);
        Assert.Equal(1, moves);
        Assert.True(request.Bounds.Height <= room.Height);
        Assert.Equal(request.Bounds.Height, content.HeightRequest, 3);
        request.Cancel();
        await showing;
    }

    /// <summary>Zooming moves composite-time properties only.</summary>
    [Fact]
    public void ZoomingRecordsNothing()
    {
        var renderer = new CountingRenderer();
        var zoom = new SkUiCoreZoomView();
        zoom.SetContent(renderer.CreateBlock("x", new SkUiMarkdownMathContext(16, Colors.Black, 16)));
        var host = new SkUiCoreHost().SetContent(zoom);
        var (frames, queue) = Show(host, 200, 100);
        Assert.Equal(1, renderer.Recordings);
        zoom.ZoomTo(3, new Point(150, 50));
        frames.RequestFrame();
        Frame(queue);
        Assert.Equal(1, renderer.Recordings);
    }
}

/// <summary>The picture popup through the app's presenter, a process-wide setting, so the class runs alone.</summary>
[Collection(GlobalStateCollection.Name)]
public class MarkdownPopupPresenterTests
{
    private const string Picture = "picture.png";

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

    private static SkUiCoreMarkdown InAWindow(bool allowZoom)
    {
        var view = new SkUiCoreMarkdown().SetAllowImageZoom(allowZoom);
        _ = new Window(new ContentPage { Content = new SkUiCoreHost().SetContent(view) });
        return view;
    }

    [Fact]
    public void AllowImageZoomOpensOnePopupAtATime()
    {
        var saved = SkUiPopups.Presenter;
        var presenter = new RecordingPopupPresenter();
        SkUiPopups.Presenter = presenter;
        try
        {
            var view = InAWindow(allowZoom: true);
            var tapped = 0;
            view.ImageTapped += (_, _) => tapped++;
            var image = SkUiImageSource.FromFile(Picture);
            view.OnImageTapped(Picture, "alt", image);
            view.OnImageTapped(Picture, "alt", image);
            Assert.Equal(2, tapped);
            var request = Assert.Single(presenter.Requests);
            request.Cancel();
        }
        finally
        {
            SkUiPopups.Presenter = saved;
        }
    }

    [Fact]
    public void WithoutAPresenterOrTheSettingOnlyTheEventRemains()
    {
        var saved = SkUiPopups.Presenter;
        var presenter = new RecordingPopupPresenter();
        try
        {
            SkUiPopups.Presenter = null;
            var view = InAWindow(allowZoom: true);
            var tapped = 0;
            view.ImageTapped += (_, _) => tapped++;
            view.OnImageTapped(Picture, "alt", SkUiImageSource.FromFile(Picture));
            Assert.Equal(1, tapped);

            SkUiPopups.Presenter = presenter;
            InAWindow(allowZoom: false).OnImageTapped(Picture, "alt", SkUiImageSource.FromFile(Picture));
            Assert.Empty(presenter.Requests);
        }
        finally
        {
            SkUiPopups.Presenter = saved;
        }
    }
}
