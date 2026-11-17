using System.Runtime.CompilerServices;
using MauiSkiaUi.Core;
using MauiSkiaUi.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace MauiSkiaUi.Tests;

/// <summary>
/// What a frame allocates once warm (NFR-2): render batches and their updates are pooled (1.14 A3), so a frame that moves
/// nodes or changes nothing allocates nothing. At 1.13.0 an idle frame allocated 408 bytes (a batch and its lists, a
/// delegate per frame request, a closure per render) and moving 50 nodes 14,720 bytes (an update per node).
/// </summary>
public sealed class RenderAllocationTests(ITestOutputHelper output)
{
    private const int Boxes = 50;

    private static (SkUiTestSurface Surface, SkUiCoreGrid Grid, SkUiCoreBox[] Boxes) Tree()
    {
        var boxes = new SkUiCoreBox[Boxes];
        var grid = new SkUiCoreGrid(); // one cell: removing a box moves no other
        for (var i = 0; i < Boxes; i++)
        {
            boxes[i] = new SkUiCoreBox();
            boxes[i].SetWidth(20).SetHeight(4).SetBackground(Colors.Red);
            grid.Add(boxes[i]);
        }
        var root = new SkUiVerticalStackLayout();
        root.Children.Add(new SkUiCoreHost { WidthRequest = 200, HeightRequest = 250 }.SetContent(grid));
        var surface = new SkUiTestSurface(root, 200, 250);
        surface.Frame(); // the first frame records every node
        return (surface, grid, boxes);
    }

    /// <summary>The fewest bytes per frame of three batches of 60 frames, after a warm-up.</summary>
    private static long BytesPerFrame(SkUiTestSurface surface, Action<int> change)
    {
        var frame = 0;
        void Step()
        {
            change(frame);
            surface.Frame(frame * 16.0);
            frame++;
        }
        for (var i = 0; i < 30; i++) Step();
        var fewest = long.MaxValue;
        for (var batch = 0; batch < 3 && fewest > 0; batch++)
        {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 60; i++) Step();
            fewest = Math.Min(fewest, GC.GetAllocatedBytesForCurrentThread() - allocated);
        }
        return fewest / 60;
    }

    [Fact]
    public void IdleFramesAllocateNothing()
    {
        var (surface, _, _) = Tree();
        using var _ = surface;

        var bytes = BytesPerFrame(surface, _ => surface.Renderer.RequestFrame());

        output.WriteLine($"idle: {bytes} bytes per frame");
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void MovingEveryNodeAllocatesNothing()
    {
        var (surface, _, boxes) = Tree();
        using var _ = surface;
        var recorded = surface.RecordedPictures;

        var bytes = BytesPerFrame(surface, frame =>
        {
            foreach (var box in boxes)
                box.SetTranslationX(frame % 2);
        });

        output.WriteLine($"moving {Boxes} nodes: {bytes} bytes per frame");
        Assert.Equal(recorded, surface.RecordedPictures); // props only: nothing re-recorded
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void APooledBatchKeepsNoRenderNodeAlive()
    {
        var (surface, grid, boxes) = Tree();
        using var _ = surface;

        var removed = MoveThenRemove(surface, grid, boxes);
        for (var frame = 1; frame <= 3; frame++)
            surface.Frame(1000 + frame * 16); // the batches that carried its update are back in the pool
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(removed.IsAlive);
    }

    /// <summary>
    /// Sends an update for every box's render node through one batch, then removes the first box; its node is only weakly
    /// held here. Its update is the first of the batch's spares, which the one-update frames after it do not reuse.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MoveThenRemove(SkUiTestSurface surface, SkUiCoreGrid grid, SkUiCoreBox[] boxes)
    {
        foreach (var each in boxes)
            each.SetTranslationX(5);
        surface.Frame(1000);
        var box = boxes[0];
        boxes[0] = null!;
        var node = new WeakReference(((ISkUiRenderable)box).RenderState.Node);
        grid.Remove(box);
        return node;
    }
}
