using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// The compositor's generic drawing primitives, on plain nodes: drawables drawn from the clock (no recording per frame, frames
/// only while drawn, owned and disposed by the compositor), a children layer the drawable blends into, and drawable spaces.
/// </summary>
public class RenderPrimitivesTests
{
    /// <summary>A drawable that draws what a test says, at the time it is drawn.</summary>
    private sealed class TestDrawable(bool animated, Action<SKCanvas, SkUiRenderDrawContext> draw) : SkUiRenderDrawable
    {
        public bool Disposed { get; private set; }

        public override bool IsAnimated => animated;

        public override void Draw(SKCanvas canvas, in SkUiRenderDrawContext context) => draw(canvas, context);

        public override void Dispose() => Disposed = true;
    }

    /// <summary>A box whose drawable a test sets.</summary>
    private sealed class Box(Color color) : SkUiCoreBox
    {
        public SkUiRenderDrawable? Drawable;

        public Box Init()
        {
            SetColor(color);
            return this;
        }

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            props.Drawable = Drawable;
        }
    }

    /// <summary>A layout whose children layer, drawable and drawable space a test sets.</summary>
    private sealed class Group : SkUiCoreAbsoluteLayout
    {
        public bool ChildrenLayer;
        public bool DrawableSpace;
        public SkUiRenderDrawable? Drawable;

        internal override void OnGetRenderProps(ref SkUiRenderProps props)
        {
            base.OnGetRenderProps(ref props);
            props.ChildrenLayer = ChildrenLayer;
            props.DrawableSpace = DrawableSpace;
            props.Drawable = Drawable;
        }
    }

    private static SkUiTestSurface Surface(SkUiCoreNode content, int width = 100, int height = 20) =>
        new(new SkUiContentView { Content = new SkUiCoreHost().SetContent(content), Background = Colors.White }, width, height);

    private static SKBitmap RenderAt(SkUiTestSurface surface, double seconds)
    {
        using var canvas = new SKCanvas(surface.Bitmap);
        surface.Renderer.Render(canvas, surface.Bitmap.Info, TimeSpan.FromSeconds(seconds));
        return surface.Bitmap;
    }

    /// <summary>A 20 DIP red square moving right by 80 DIPs a second (wrapping every second), in local coordinates.</summary>
    private static TestDrawable Mover() => new(animated: true, (canvas, context) =>
    {
        context.Paint.Color = SKColors.Red;
        var x = (float)(context.Now.TotalSeconds % 1 * 80);
        canvas.DrawRect(SKRect.Create(x, 0, 20, 20), context.Paint);
    });

    [Fact]
    public void ADrawableMovesWithTheClockWithoutRecording()
    {
        var box = new Box(Colors.Transparent).Init();
        box.Drawable = Mover();
        var group = new Group();
        group.Add(box, new Rect(0, 0, 100, 20));
        using var surface = Surface(group);
        surface.Frame();
        Assert.True(surface.NeedsFrame);
        var recorded = surface.RecordedPictures;
        Assert.Equal(SKColors.Red, RenderAt(surface, 10.25).GetPixel(30, 10)); // at x 20..40
        Assert.Equal(SKColors.White, surface.Bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.Red, RenderAt(surface, 10.75).GetPixel(70, 10)); // at x 60..80
        Assert.Equal(recorded, surface.RecordedPictures);
    }

    [Fact]
    public void ADrawableRequestsFramesOnlyWhileItIsDrawnAndAnimated()
    {
        var box = new Box(Colors.Red).Init();
        box.Drawable = Mover();
        var inner = new Group();
        inner.Add(box, new Rect(0, 0, 20, 20));
        var outer = new Group();
        outer.Add(inner, new Rect(0, 0, 20, 20));
        using var surface = Surface(outer);
        surface.Frame();
        Assert.True(surface.NeedsFrame);

        outer.SetLayoutBounds(inner, new Rect(500, 0, 20, 20)); // out of view: the parent is culled
        surface.Frame(16);
        surface.Frame(32);
        Assert.False(surface.NeedsFrame);

        // Back in view, still: drawn without asking for frames.
        outer.SetLayoutBounds(inner, new Rect(0, 0, 20, 20));
        box.Drawable = new TestDrawable(animated: false, (canvas, context) => canvas.DrawRect(context.Bounds, context.Paint));
        box.InvalidateRender(SkUiRenderDirty.Props);
        surface.Frame(48);
        surface.Frame(64);
        Assert.False(surface.NeedsFrame);
    }

    [Fact]
    public void TheCompositorDisposesReplacedAndRemovedDrawables()
    {
        var first = Mover();
        var box = new Box(Colors.Red).Init();
        box.Drawable = first;
        var group = new Group();
        group.Add(box, new Rect(0, 0, 20, 20));
        using var surface = Surface(group);
        surface.Frame();
        surface.Frame(16); // committed again unchanged: kept
        Assert.False(first.Disposed);

        var second = Mover();
        box.Drawable = second;
        box.InvalidateRender(SkUiRenderDirty.Props);
        surface.Frame(32);
        Assert.True(first.Disposed);
        Assert.False(second.Disposed);

        group.Remove(box);
        surface.Frame(48);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void TheDrawableBlendsIntoTheChildrenLayerOnly()
    {
        // A red bone, then the group's drawable fills everything blue, source-in: blue only where the bone is.
        var group = new Group
        {
            ChildrenLayer = true,
            Drawable = new TestDrawable(false, (canvas, context) =>
            {
                context.Paint.Color = SKColors.Blue;
                context.Paint.BlendMode = SKBlendMode.SrcIn;
                canvas.DrawRect(context.Bounds, context.Paint);
            })
        };
        group.Add(new Box(Colors.Red).Init(), new Rect(0, 0, 50, 20));
        using var surface = Surface(group);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(75, 10)); // the white background outside the layer is untouched

        // Without the layer the blend reaches what is under the group (the background turns blue too).
        group.ChildrenLayer = false;
        group.InvalidateRender(SkUiRenderDirty.Props);
        Assert.Equal(SKColors.Blue, surface.Frame(16).GetPixel(75, 10));
    }

    [Fact]
    public void DrawablesArePositionedInTheNearestDrawableSpace()
    {
        // The box (not clipping) draws a 10 DIP square at the origin of its drawable space.
        var box = new Box(Colors.Transparent).Init();
        box.ClipToBounds = false;
        box.Drawable = new TestDrawable(false, (canvas, context) =>
        {
            context.Paint.Color = SKColors.Red;
            canvas.SetMatrix(context.Space);
            canvas.DrawRect(SKRect.Create(10, 10), context.Paint);
        });
        var space = new Group { DrawableSpace = true, ClipToBounds = false };
        space.Add(box, new Rect(40, 0, 50, 20)); // the box elsewhere in the space
        var outer = new Group();
        outer.Add(space, new Rect(20, 0, 80, 20));
        using var surface = Surface(outer);
        var bitmap = surface.Frame();
        Assert.Equal(SKColors.Red, bitmap.GetPixel(25, 5)); // the space's origin, not the box's
        Assert.Equal(SKColors.White, bitmap.GetPixel(45, 5));

        // The space moves with its node on the render thread.
        space.TranslationX = 30;
        Assert.Equal(SKColors.Red, surface.Frame(16).GetPixel(55, 5));

        // Without a space: the surface.
        space.DrawableSpace = false;
        space.InvalidateRender(SkUiRenderDirty.Props);
        bitmap = surface.Frame(32);
        Assert.Equal(SKColors.Red, bitmap.GetPixel(5, 5));
        Assert.Equal(SKColors.White, bitmap.GetPixel(55, 5));
    }

    [Fact]
    public void TheImmediatePainterDrawsDrawablesAtTimeZero()
    {
        var box = new Box(Colors.Transparent).Init();
        box.Drawable = Mover();
        var group = new Group();
        group.Add(box, new Rect(0, 0, 100, 20));
        group.Measure(100, 20);
        group.Arrange(new Rect(0, 0, 100, 20));
        using var bitmap = new SKBitmap(100, 20);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            group.Paint(canvas);
        }
        Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        Assert.Equal(SKColors.White, bitmap.GetPixel(30, 10));
    }
}
