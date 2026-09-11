using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

public class PipelineTests
{
    [Fact]
    public void InvalidationDuringRecordingSchedulesOneFollowupWithoutAnimation()
    {
        var root = new InvalidatingPaintProbe { Color = Colors.Red };
        Arrange(root, 40, 40);
        var queue = new Queue<Action>();
        var presents = 0;
        using var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => presents++, () => { });
        renderer.RequestFrame();
        renderer.RequestFrame();
        Assert.Single(queue);
        queue.Dequeue()();
        Assert.Single(queue);
        Assert.Equal(1, presents);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        renderer.Replay(canvas, bitmap.Info);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));

        queue.Dequeue()();
        renderer.Replay(canvas, bitmap.Info);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 20));
        Assert.Equal(2, presents);
        Assert.Empty(queue);
        Assert.False(root.AnimationClock.IsRunning);
    }

    [Fact]
    public void ChangesBeforePaintingAreIncludedWithoutRedundantFrame()
    {
        var root = new SkUiBox { Color = Colors.Red };
        Arrange(root, 40, 40);
        var queue = new Queue<Action>();
        using var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => { }, () => root.Color = Colors.Blue);
        renderer.RequestFrame();
        queue.Dequeue()();
        Assert.Empty(queue);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        renderer.Replay(canvas, bitmap.Info);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 20));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FrameReplayAndPixelTouchAgreeAtDifferentDensities(int density)
    {
        var root = new SkUiBox
        {
            Color = Colors.Red, AnchorX = 0, AnchorY = 0,
            TranslationX = 10, TranslationY = 5, Scale = 0.5
        };
        ((IView)root).Measure(100, 80);
        ((IView)root).Arrange(new Rect(30, 40, 100, 80));
        var queue = new Queue<Action>();
        using var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => { }, () => { });
        renderer.RequestFrame();
        queue.Dequeue()();
        using var bitmap = new SKBitmap(100 * density, 80 * density);
        using var canvas = new SKCanvas(bitmap);
        var matrix = canvas.TotalMatrix;
        var saves = canvas.SaveCount;
        renderer.Replay(canvas, bitmap.Info);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20 * density, 15 * density));
        Assert.Equal(0, bitmap.GetPixel(5 * density, 5 * density).Alpha);
        Assert.Equal(matrix, canvas.TotalMatrix);
        Assert.Equal(saves, canvas.SaveCount);
        Point? tappedAt = null;
        root.Tapped += (_, args) => tappedAt = args.Position;
        var pixelPosition = new Point(20 * density, 15 * density);
        Assert.True(renderer.TouchPixels(new(1, SkUiTouchAction.Pressed, pixelPosition)));
        Assert.True(renderer.TouchPixels(new(1, SkUiTouchAction.Released, pixelPosition)));
        Assert.Equal(new Point(20, 20), tappedAt);
    }

    [Fact]
    public void DisposingRendererStopsClockReleasesPictureAndIgnoresQueuedWork()
    {
        var root = new SkUiBox();
        Arrange(root, 40, 40);
        var queue = new Queue<Action>();
        var presents = 0;
        var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => presents++, () => { });
        renderer.RequestFrame();
        queue.Dequeue()();
        root.AnimationClock.Start(_ => { }, TimeSpan.FromSeconds(1));
        root.InvalidatePaint();
        Assert.Single(queue);
        renderer.Dispose();
        renderer.Dispose();
        Assert.False(root.AnimationClock.IsRunning);
        queue.Dequeue()();
        root.InvalidatePaint();
        Assert.Empty(queue);
        Assert.Equal(1, presents);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        renderer.Replay(canvas, bitmap.Info);
        Assert.Equal(0, bitmap.GetPixel(20, 20).Alpha);
        Assert.False(renderer.TouchPixels(new(1, SkUiTouchAction.Pressed, new Point(20, 20))));
    }

    [Fact]
    public void FrameRendererRejectsHostedRoots()
    {
        var child = new SkUiBox();
        var host = new SkUiContentView { Content = child };
        Assert.Throws<InvalidOperationException>(() => new SkUiFrameRenderer(child, _ => { }, () => { }, () => { }));
        Assert.Same(host, child.Parent);
    }

    [Fact]
    public void ZeroSizedRootCanRenderAfterLayoutInvalidation()
    {
        var root = new SkUiBox();
        var queue = new Queue<Action>();
        var presents = 0;
        using var renderer = new SkUiFrameRenderer(root, queue.Enqueue, () => presents++, () => { });
        renderer.RequestFrame();
        queue.Dequeue()();
        Assert.Equal(0, presents);
        Assert.Empty(queue);
        Arrange(root, 40, 40);
        Assert.Single(queue);
        queue.Dequeue()();
        Assert.Equal(1, presents);
    }

    private sealed class InvalidatingPaintProbe : SkUiBox
    {
        protected override void OnPaintOverlay(SKCanvas canvas)
        {
            if (Color == Colors.Red)
            {
                Color = Colors.Blue;
                InvalidatePaint();
            }
        }
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(20, 0)]
    public void NestedMarginedTransformedLayoutRemapsCapturedPointer(double movement, int expectedTaps)
    {
        var child = new SkUiBox
        {
            WidthRequest = 40, HeightRequest = 40, Margin = new Thickness(10, 15, 0, 0),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start
        };
        var layout = new SkUiLayout
        {
            WidthRequest = 120, HeightRequest = 100, Margin = new Thickness(30, 40, 0, 0),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            AnchorX = 0, AnchorY = 0, TranslationX = 100, TranslationY = 10, Scale = 2, Rotation = 90
        };
        layout.Children.Add(child);
        var host = new SkUiContentView { Content = layout };
        Arrange(host, 400, 400);
        Assert.Equal(new Rect(30, 40, 120, 100), layout.Frame);
        Assert.Equal(new Rect(10, 15, 40, 40), child.Frame);
        var taps = new List<Point>();
        child.Tapped += (_, args) => taps.Add(args.Position);

        Assert.True(host.Touch(new(7, SkUiTouchAction.Pressed, new Point(60, 110))));
        Assert.True(host.Touch(new(7, SkUiTouchAction.Moved, new Point(60, 110 + 2 * movement))));
        Assert.True(host.Touch(new(7, SkUiTouchAction.Released, new Point(60, 110))));

        Assert.Equal(expectedTaps, taps.Count);
        if (expectedTaps > 0)
        {
            Assert.Equal(20, taps[0].X, 3);
            Assert.Equal(20, taps[0].Y, 3);
        }
    }

    [Fact]
    public void InvisibleNodeIsCollapsedAndPassesInputToVisibleSibling()
    {
        var bottom = new SkUiBox { Color = Colors.Red };
        var top = new SkUiBox { Color = Colors.Blue, IsVisible = false };
        var layout = new SkUiLayout();
        layout.Children.Add(bottom);
        layout.Children.Add(top);
        var taps = 0;
        bottom.Tapped += (_, _) => taps++;
        top.Tapped += (_, _) => throw new InvalidOperationException("Invisible node received a tap.");
        Arrange(layout, 100, 100);
        Assert.Equal(Visibility.Collapsed, ((IView)top).Visibility);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        layout.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Tap(layout, new Point(10, 10));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void DefaultMaximumSizesAreUnboundedAndMeasureFiniteContent()
    {
        IView node = new SkUiBox();
        Assert.Equal(double.PositiveInfinity, node.MaximumWidth);
        Assert.Equal(double.PositiveInfinity, node.MaximumHeight);
        Assert.Equal(new Size(48, 48), node.Measure(double.PositiveInfinity, double.PositiveInfinity));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ExplicitNaNMaximumDoesNotReachContentMeasure(bool unsetWidth, bool unsetHeight)
    {
        var node = new LayoutProbe
        {
            MaximumWidthRequest = unsetWidth ? double.NaN : 80,
            MaximumHeightRequest = unsetHeight ? double.NaN : 90
        };
        Assert.Equal(new Size(48, 48), ((IView)node).Measure(100, 120));
        Assert.Equal(new Size(unsetWidth ? 100 : 80, unsetHeight ? 120 : 90), node.LastConstraint);
    }

    [Fact]
    public void EllipseCornerUsesArrangedBoundsForTaps()
    {
        var ellipse = new SkUiEllipse();
        var host = new SkUiContentView { Content = ellipse };
        Arrange(host, 100, 100);
        var taps = 0;
        ellipse.Tapped += (_, _) => taps++;
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        host.Paint(canvas);
        Assert.Equal(0, bitmap.GetPixel(1, 1).Alpha);
        Tap(host, new Point(1, 1));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void RestartAfterStopAllUsesExistingMonotonicTimeline()
    {
        var clock = new SkUiAnimationClock();
        clock.Start(_ => { }, TimeSpan.FromSeconds(10));
        clock.Tick(TimeSpan.FromSeconds(5));
        clock.StopAll();
        var value = -1d;
        using var animation = clock.Start(progress => value = progress, TimeSpan.FromSeconds(2));
        Assert.Equal(0, value);
        clock.Tick(TimeSpan.FromSeconds(6));
        Assert.Equal(0.5, value);
        clock.Tick(TimeSpan.FromSeconds(7));
        Assert.Equal(1, value);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void PrimitivesPaintEllipseAndLineGeometry()
    {
        var ellipse = new SkUiEllipse { Color = Colors.Blue, WidthRequest = 40, HeightRequest = 40 };
        Arrange(ellipse, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        ellipse.Paint(canvas);
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(20, 20));
        Assert.Equal(0, bitmap.GetPixel(0, 0).Alpha);

        var line = new SkUiLine { Color = Colors.Red, StrokeWidth = 4 };
        Arrange(line, 40, 40);
        canvas.Clear(SKColors.Transparent);
        line.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
        Assert.Equal(0, bitmap.GetPixel(30, 10).Alpha);
    }

    [Fact]
    public void PaintRespectsZOrderOpacityAndRestoresCanvasState()
    {
        var layout = new SkUiLayout();
        layout.Children.Add(new SkUiBox { Color = Colors.Blue, Opacity = 0.5, ZIndex = 2 });
        layout.Children.Add(new SkUiBox { Color = Colors.Red });
        Arrange(layout, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        var matrix = canvas.TotalMatrix;
        var saves = canvas.SaveCount;
        layout.Paint(canvas);
        var pixel = bitmap.GetPixel(20, 20);
        Assert.InRange(pixel.Red, (byte)127, (byte)128);
        Assert.InRange(pixel.Blue, (byte)127, (byte)128);
        Assert.Equal(255, pixel.Alpha);
        Assert.Equal(matrix, canvas.TotalMatrix);
        Assert.Equal(saves, canvas.SaveCount);
    }

    [Fact]
    public void PaintLayersAreOrderedAndInvisibleNodesDoNotPaint()
    {
        var calls = new List<string>();
        var probe = new PaintProbe(calls);
        Arrange(probe, 40, 40);
        using var bitmap = new SKBitmap(40, 40);
        using var canvas = new SKCanvas(bitmap);
        probe.Paint(canvas);
        Assert.Equal(["background", "content", "overlay"], calls);
        calls.Clear();
        probe.IsVisible = false;
        probe.Paint(canvas);
        Assert.Empty(calls);
    }

    [Fact]
    public void DirtyChildRemeasuresWithoutRemeasuringCleanSibling()
    {
        var first = new LayoutProbe();
        var second = new LayoutProbe();
        var layout = new SkUiLayout();
        layout.Children.Add(first);
        layout.Children.Add(second);
        var host = new SkUiContentView { Content = layout };
        Arrange(host, 100, 100);
        Arrange(host, 100, 100);
        Assert.Equal(1, first.Measures);
        Assert.Equal(1, first.Arranges);
        Assert.Equal(1, second.Measures);
        first.WidthRequest = 30;
        Arrange(host, 100, 100);
        Assert.Equal(2, first.Measures);
        Assert.Equal(1, second.Measures);
        Assert.Equal(30, first.Width);
    }

    [Fact]
    public void TransformAnimationDoesNotRemeasureOrRearrange()
    {
        var child = new LayoutProbe();
        var host = new SkUiContentView { Content = child };
        Arrange(host, 100, 100);
        var invalidations = 0;
        host.PaintInvalidated += (_, _) => invalidations++;
        using var animation = child.AnimationClock.Start(progress => child.TranslationX = 20 * progress, TimeSpan.FromSeconds(1));
        child.AnimationClock.Tick(TimeSpan.FromMilliseconds(500));
        Arrange(host, 100, 100);
        Assert.Equal(10, child.TranslationX);
        Assert.Equal(1, child.Measures);
        Assert.Equal(1, child.Arranges);
        Assert.True(invalidations > 0);
        Assert.Same(host.AnimationClock, child.AnimationClock);
    }

    [Fact]
    public void UpdateBatchesCoalesceAndSizeChangePropagates()
    {
        var child = new SkUiBox();
        var host = new SkUiContentView { Content = child };
        Arrange(host, 100, 100);
        var invalidations = 0;
        host.PaintInvalidated += (_, _) => invalidations++;
        child.StartUpdating();
        child.StartUpdating();
        child.Color = Colors.Red;
        child.WidthRequest = 30;
        child.EndUpdating();
        Assert.Equal(0, invalidations);
        child.EndUpdating();
        Assert.Equal(1, invalidations);
        Arrange(host, 100, 100);
        Assert.Equal(30, child.Width);
        Assert.Throws<InvalidOperationException>(child.EndUpdating);
    }

    [Fact]
    public void OwnershipRejectsDuplicatesAndCyclesAndInheritsBindingContext()
    {
        var context = new object();
        var child = new SkUiBox();
        var layout = new SkUiLayout { BindingContext = context };
        layout.Children.Add(child);
        Assert.Same(layout, child.Parent);
        Assert.Same(context, child.BindingContext);
        Assert.Throws<InvalidOperationException>(() => layout.Children.Add(child));
        Assert.Throws<InvalidOperationException>(() => layout.Children.Add(layout));
        var host = new SkUiContentView { Content = layout };
        Assert.Throws<InvalidOperationException>(() => layout.Children.Add(host));
        layout.Children.Remove(child);
        Assert.Null(child.Parent);
        Assert.Null(child.BindingContext);
        host.Content = child;
        Assert.Null(layout.Parent);
        Assert.Same(host, child.Parent);
    }

    [Theory]
    [InlineData(false, true, false, 1)]
    [InlineData(true, false, false, 1)]
    [InlineData(false, false, true, 0)]
    public void PassiveTransparentAndDisabledNodesFollowHitRules(bool transparent, bool passive, bool disabled, int expectedTaps)
    {
        var bottom = new SkUiBox();
        var top = new SkUiEllipse { InputTransparent = transparent, IsEnabled = !disabled };
        var layout = new SkUiLayout();
        layout.Children.Add(bottom);
        layout.Children.Add(top);
        var taps = 0;
        bottom.Tapped += (_, _) => taps++;
        if (!passive)
            top.Tapped += (_, _) => throw new InvalidOperationException("Top view must not receive a tap.");
        Arrange(layout, 100, 100);
        Tap(layout, new Point(1, 1));
        Assert.Equal(expectedTaps, taps);
    }

    [Fact]
    public void TransformedPrimitivePaintAndHitCoordinatesAgree()
    {
        var box = new SkUiBox
        {
            Color = Colors.Red, WidthRequest = 20, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start,
            TranslationX = 30, TranslationY = 20, Scale = 2, Rotation = 90
        };
        var host = new SkUiContentView { Content = box };
        Arrange(host, 100, 100);
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        host.Paint(canvas);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(40, 30));
        Assert.Equal(0, bitmap.GetPixel(5, 5).Alpha);
        Point? tappedAt = null;
        box.Tapped += (_, args) => tappedAt = args.Position;
        Tap(host, new Point(40, 30));
        Assert.NotNull(tappedAt);
        Assert.Equal(10, tappedAt.Value.X, 3);
        Assert.Equal(10, tappedAt.Value.Y, 3);
    }

    [Theory]
    [InlineData(SkUiTouchAction.Moved)]
    [InlineData(SkUiTouchAction.Cancelled)]
    public void MovementAndCancellationSuppressTaps(SkUiTouchAction action)
    {
        var child = new SkUiBox();
        var host = new SkUiContentView { Content = child };
        Arrange(host, 100, 100);
        var taps = 0;
        child.Tapped += (_, _) => taps++;
        Assert.True(host.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10))));
        Assert.False(host.Touch(new(2, SkUiTouchAction.Released, new Point(10, 10))));
        Assert.True(host.Touch(new(1, action, new Point(80, 80))));
        host.Touch(new(1, SkUiTouchAction.Released, new Point(10, 10)));
        Assert.Equal(0, taps);
        Tap(host, new Point(10, 10));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void RemovingCapturedChildCancelsItsTap()
    {
        var child = new SkUiBox();
        var layout = new SkUiLayout();
        layout.Children.Add(child);
        Arrange(layout, 100, 100);
        var taps = 0;
        child.Tapped += (_, _) => taps++;
        layout.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10)));
        layout.Children.Remove(child);
        child.Touch(new(1, SkUiTouchAction.Released, new Point(10, 10)));
        Assert.Equal(0, taps);
    }

    [Fact]
    public void DisablingContainerDuringCaptureCancelsTap()
    {
        var child = new SkUiBox();
        var host = new SkUiContentView { Content = child };
        Arrange(host, 100, 100);
        var taps = 0;
        child.Tapped += (_, _) => taps++;
        host.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10)));
        host.IsEnabled = false;
        host.Touch(new(1, SkUiTouchAction.Released, new Point(10, 10)));
        Assert.Equal(0, taps);
        host.IsEnabled = true;
        Tap(host, new Point(10, 10));
        Assert.Equal(1, taps);
    }

    [Fact]
    public void AnimationTickDoesNotAdvanceAnimationsStartedDuringSameTick()
    {
        var clock = new SkUiAnimationClock();
        var secondApplies = 0;
        clock.Start(progress =>
        {
            if (progress >= 0.5 && secondApplies == 0)
                clock.Start(_ => secondApplies++, TimeSpan.FromSeconds(1));
        }, TimeSpan.FromSeconds(1));
        clock.Tick(TimeSpan.FromMilliseconds(500));
        // Start already invoked apply(0) once; the same Tick must not invoke it again.
        Assert.Equal(1, secondApplies);
        clock.Tick(TimeSpan.FromMilliseconds(750));
        Assert.Equal(2, secondApplies);
    }

    [Fact]
    public void AnimationRepeatCancellationAndMonotonicTimeAreDeterministic()
    {
        var clock = new SkUiAnimationClock();
        var transitions = 0;
        var value = 0d;
        clock.RunningChanged += (_, _) => transitions++;
        var animation = clock.Start(progress => value = progress, TimeSpan.FromSeconds(1), repeat: true);
        clock.Tick(TimeSpan.FromMilliseconds(1250));
        Assert.Equal(0.25, value);
        Assert.True(clock.IsRunning);
        animation.Dispose();
        animation.Dispose();
        Assert.False(clock.IsRunning);
        Assert.Equal(2, transitions);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Start(_ => { }, TimeSpan.Zero));
    }

    private static void Arrange(IView view, double width, double height)
    {
        view.Measure(width, height);
        view.Arrange(new Rect(0, 0, width, height));
    }

    private static void Tap(ISkUiView view, Point point)
    {
        Assert.True(view.Touch(new(1, SkUiTouchAction.Pressed, point)));
        Assert.True(view.Touch(new(1, SkUiTouchAction.Released, point)));
    }

    private sealed class LayoutProbe : SkUiBox
    {
        public int Measures { get; private set; }
        public int Arranges { get; private set; }
        public Size LastConstraint { get; private set; }

        protected override Size MeasureContent(double widthConstraint, double heightConstraint)
        {
            Measures++;
            LastConstraint = new Size(widthConstraint, heightConstraint);
            return base.MeasureContent(widthConstraint, heightConstraint);
        }

        protected override void ArrangeContent(Size size) => Arranges++;
    }

    private sealed class PaintProbe(List<string> calls) : SkUiView
    {
        protected override void OnPaintBackground(SKCanvas canvas) => calls.Add("background");
        protected override void OnPaintContent(SKCanvas canvas) => calls.Add("content");
        protected override void OnPaintOverlay(SKCanvas canvas) => calls.Add("overlay");
    }

    [Fact]
    public void HostedPrimitivesPaintAndReceiveTapsWithoutHandlers()
    {
        var box = new SkUiBox { Color = Colors.Red, WidthRequest = 40, HeightRequest = 30, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start };
        var layout = new SkUiLayout();
        layout.Children.Add(box);
        var host = new SkUiContentView { Content = layout };
        IView root = host;
        root.Measure(100, 100);
        root.Arrange(new Rect(0, 0, 100, 100));
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        host.Paint(canvas);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Assert.Equal(0, bitmap.GetPixel(60, 60).Alpha);
        var taps = 0;
        box.Tapped += (_, _) => taps++;
        Assert.True(host.Touch(new(1, SkUiTouchAction.Pressed, new Point(10, 10))));
        Assert.True(host.Touch(new(1, SkUiTouchAction.Released, new Point(10, 10))));
        Assert.Equal(1, taps);
        Assert.Null(box.Handler);
        Assert.Null(layout.Handler);
    }

    [Fact]
    public void AnimationTicksDeterministicallyAndStopsAtCompletion()
    {
        var clock = new SkUiAnimationClock();
        var value = -1d;
        using var animation = clock.Start(progress => value = progress, TimeSpan.FromSeconds(1));
        Assert.Equal(0, value);
        Assert.True(clock.IsRunning);
        clock.Tick(TimeSpan.FromMilliseconds(500));
        Assert.Equal(0.5, value);
        clock.Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(1, value);
        Assert.False(clock.IsRunning);
    }

    [Fact]
    public void HandlerlessViewMeasuresAndArrangesWithMarginsAndAlignment()
    {
        var node = new SkUiView
        {
            WidthRequest = 80,
            HeightRequest = 40,
            Margin = new Thickness(10),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        IView view = node;

        Assert.Equal(new Size(100, 60), view.Measure(200, 100));
        view.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(new Rect(60, 30, 80, 40), view.Frame);
        Assert.Null(node.Handler);
    }
}