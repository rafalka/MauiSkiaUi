using System.Reflection;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp;
using Xunit;

namespace MauiSkiaUi.Tests;

/// <summary>
/// Paths and shadow objects committed to the compositor (clip paths, shadow outlines and styles, a border's content clip, an
/// indeterminate progress bar's slide clip) are disposed by the compositor, not the finalizer: when a commit replaces them,
/// when their node is removed (its UI node then forgets them and makes new ones) and when the compositor is disposed.
/// Paths only recording uses (rounded text clips, border outlines) are disposed when their node is reset.
/// </summary>
[Collection(RuntimeXamlCollection.Name)]
public class VisualEffectsDisposalTests
{
    private static SkUiRenderProps Committed(ISkUiRenderable node) => node.RenderState.Node.Props;

    private static bool Disposed(SKObject? native) => native is null || native.Handle == IntPtr.Zero;

    /// <summary>A border with a clip, a shadow (an opaque fill: an outline) and a rounded stroke (a content clip).</summary>
    private static SkUiBorder Border() => new()
    {
        HeightRequest = 60,
        BackgroundColor = Colors.White,
        Stroke = Colors.Gray,
        StrokeThickness = 2,
        StrokeShape = new RoundRectangle { CornerRadius = 10 },
        Clip = new RoundRectangleGeometry(new CornerRadius(8), new Rect(0, 0, 100, 60)),
        Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(2, 2), Radius = 6 },
        Content = new SkUiBox { Color = Colors.Red }
    };

    private static SkUiFrameRenderer Surface(SkUiView root)
    {
        var renderer = new SkUiFrameRenderer(root, _ => { }, () => { }, () => { });
        Frame(renderer, root);
        return renderer;
    }

    /// <summary>Records what changed and applies it on the "render thread" (compositing).</summary>
    private static void Frame(SkUiFrameRenderer renderer, SkUiView root)
    {
        SkUiTestHelpers.Arrange(root, 200, 300);
        renderer.PresentFrame();
        using var bitmap = new SKBitmap(200, 300);
        using var canvas = new SKCanvas(bitmap);
        renderer.Render(canvas, bitmap.Info, TimeSpan.Zero);
    }

    [Fact]
    public void ARemovedNodesEffectsAreDisposedWhenTheRemovalIsAppliedAndMadeAnewWhenItIsBack()
    {
        var border = Border();
        var root = new SkUiVerticalStackLayout { Children = { border } };
        using var surface = Surface(root);
        var props = Committed(border);
        Assert.NotNull(props.ClipPath);
        Assert.NotNull(props.ChildrenClipPath);
        Assert.NotNull(props.Shadow?.Outline);
        Assert.False(Disposed(props.ClipPath));

        root.Children.Remove(border);
        Assert.False(Disposed(props.ClipPath)); // the render thread may still draw them until the removal is applied
        Frame(surface, root);
        Assert.True(Disposed(props.ClipPath));
        Assert.True(Disposed(props.ChildrenClipPath));
        Assert.True(Disposed(props.Shadow!.Outline));

        root.Children.Add(border);
        Frame(surface, root);
        var again = Committed(border);
        Assert.NotSame(props.ClipPath, again.ClipPath);
        Assert.False(Disposed(again.ClipPath));
        Assert.False(Disposed(again.ChildrenClipPath));
        Assert.False(Disposed(again.Shadow?.Outline));
    }

    [Fact]
    public void AReplacedContentClipIsDisposedAndTearingDownDisposesTheRest()
    {
        var border = Border();
        var bar = new SkUiProgressBar { IsIndeterminate = true, HeightRequest = 8 };
        var root = new SkUiVerticalStackLayout { Children = { border, bar } };
        var surface = Surface(root);
        var clip = Committed(border).ChildrenClipPath;
        var slide = Committed(bar).ContentClipPath;
        Assert.NotNull(slide);

        border.StrokeThickness = 6; // a new content clip
        Frame(surface, root);
        Assert.True(Disposed(clip));
        var committed = Committed(border);
        Assert.False(Disposed(committed.ChildrenClipPath));

        surface.Dispose();
        Assert.True(Disposed(committed.ChildrenClipPath));
        Assert.True(Disposed(committed.ClipPath));
        Assert.True(Disposed(committed.Shadow!.Outline));
        Assert.True(Disposed(slide));

        // A new surface records everything again from new objects.
        using var next = Surface(root);
        Assert.False(Disposed(Committed(border).ClipPath));
        Assert.False(Disposed(Committed(bar).ContentClipPath));
    }

    [Fact]
    public void ActivityIndicatorsDisposeTheirStrokePaintWhenTheyLeave()
    {
        var indicator = new SkUiActivityIndicator { IsRunning = true, WidthRequest = 40, HeightRequest = 40 };
        var core = new SkUiCoreActivityIndicator().SetIsRunning(true);
        var host = new SkUiCoreHost();
        host.SetContent(core);
        var root = new SkUiVerticalStackLayout { Children = { indicator, host } };
        using var surface = Surface(root);
        SKPaint? Paint(object owner) => (SKPaint?)owner.GetType().GetField("_strokePaint", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);
        var (drawn, coreDrawn) = (Paint(indicator), Paint(core));
        Assert.False(Disposed(drawn));
        Assert.False(Disposed(coreDrawn));

        root.Children.Remove(indicator);
        host.SetContent(null);
        Assert.True(Disposed(drawn));
        Assert.True(Disposed(coreDrawn));
        Assert.Null(Paint(indicator));
        root.Children.Add(indicator);
        Frame(surface, root);
        Assert.False(Disposed(Paint(indicator)));
    }

    [Fact]
    public void PathsOnlyRecordingUsesAreDisposedWhenTheNodeLeaves()
    {
        using var _ = SkUiTestHelpers.UseBundledFont(); // Linux agents have no system fonts: the label would be empty
        var label = new SkUiLabel { Text = "Chip", FontFamily = SkUiTestHelpers.BundledFontFamily, BackgroundColor = Colors.Yellow, CornerRadius = 8 };
        var root = new SkUiVerticalStackLayout { Children = { label } };
        using var surface = Surface(root);
        var chrome = typeof(SkUiLabel).GetField("_chrome", BindingFlags.NonPublic | BindingFlags.Instance)!;
        SKPath? RoundedClip()
        {
            var state = chrome.GetValue(label)!;
            var clip = state.GetType().GetField("_clip", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(state)!;
            return (SKPath?)clip.GetType().GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(clip);
        }
        var path = RoundedClip();
        Assert.False(Disposed(path)); // text was clipped to the rounded corners

        root.Children.Remove(label);
        Assert.True(Disposed(path));
        Assert.Null(RoundedClip());
        root.Children.Add(label);
        Frame(surface, root);
        Assert.False(Disposed(RoundedClip()));
    }
}
